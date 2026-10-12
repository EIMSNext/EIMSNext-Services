using System.Collections;
using System.Globalization;
using System.Text.Json;
using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Extensions;
using EIMSNext.Entities;

namespace EIMSNext.Component
{
    public static class FormDataFormatter
    {
        /// <summary>地址对象的字段名，拼接顺序即显示顺序（省 + 市 + 区 + 详细地址）。</summary>
        private static readonly string[] AddressKeys = ["province", "city", "district", "detail"];

        public static Dictionary<string, object?> Format(FormData data, IList<FieldDef> fieldDefs)
        {
            var resultDict = new Dictionary<string, object?>();

            resultDict[Fields.CreateBy] = data.CreateBy;
            resultDict[Fields.CreateTime] = FormatTimestamp(data.CreateTime, Constants.Defaut_DateFormat);
            resultDict[Fields.UpdateBy] = data.UpdateBy;
            resultDict[Fields.UpdateTime] = data.UpdateTime.HasValue
                ? FormatTimestamp(data.UpdateTime.Value, Constants.Defaut_DateFormat)
                : string.Empty;

            var fieldMap = fieldDefs
                .Where(x => !string.IsNullOrWhiteSpace(x.Field))
                .ToDictionary(x => x.Field, StringComparer.OrdinalIgnoreCase);

            var dataDict = data.Data;
            foreach (var item in dataDict)
            {
                if (!fieldMap.TryGetValue(item.Key, out var fieldDef))
                {
                    continue;
                }

                resultDict[item.Key] = FormatFieldValue(item.Value, fieldDef);
            }

            return resultDict;
        }

        public static Dictionary<string, object?> FormatForDisplay(FormData data, IList<FieldDef> fieldDefs)
        {
            var resultDict = new Dictionary<string, object?>();

            var fieldMap = fieldDefs
                .Where(x => !string.IsNullOrWhiteSpace(x.Field))
                .ToDictionary(x => x.Field, StringComparer.OrdinalIgnoreCase);

            var dataDict = data.Data;
            foreach (var item in dataDict)
            {
                if (!fieldMap.TryGetValue(item.Key, out var fieldDef))
                {
                    continue;
                }

                resultDict[item.Key] = FormatDisplayFieldValue(item.Value, fieldDef);
            }

            return resultDict;
        }

        private static object? FormatFieldValue(object? value, FieldDef fieldDef)
        {
            if (value == null)
            {
                return null;
            }

            return fieldDef.Type switch
            {
                FieldType.TimeStamp => FormatTimestampValue(value, fieldDef.Props.Format),
                FieldType.Number => FormatNumberValue(value, fieldDef.Props.Format),
                FieldType.Address => FormatAddressValue(value),
                FieldType.TableForm => FormatTableFormValue(value, fieldDef.Columns),
                _ => value,
            };
        }

        private static object? FormatDisplayFieldValue(object? value, FieldDef fieldDef)
        {
            if (value == null)
            {
                return null;
            }

            return fieldDef.Type switch
            {
                FieldType.TimeStamp => FormatTimestampValue(value, fieldDef.Props.Format),
                FieldType.Number => FormatNumberValue(value, fieldDef.Props.Format),
                FieldType.Radio or FieldType.CheckBox or FieldType.Select1 or FieldType.Select2
                    or FieldType.Employee1 or FieldType.Employee2
                    or FieldType.Department1 or FieldType.Department2
                    => FormatLabelValue(value),
                FieldType.ImageUpload or FieldType.FileUpload => FormatFileValue(value),
                FieldType.DataSelect => FormatDataSelectValue(value),
                FieldType.Address => FormatAddressValue(value),
                FieldType.TableForm => FormatDisplayTableFormValue(value, fieldDef.Columns),
                _ => value,
            };
        }

        /// <summary>
        /// 地址 { province, city, district, detail } → "省市区详细地址"。
        /// 与打印规则一致：province + city + district + detail 直接拼接，不加分隔符。
        /// 兼容历史数组值 ["省","市","区"] → "省市区"。
        /// </summary>
        private static object? FormatAddressValue(object? value)
        {
            if (value == null)
            {
                return null;
            }

            if (value.AsDictionary() is { } dict)
            {
                return string.Concat(AddressKeys.Select(name =>
                    dict.TryGetValue(name, out var item) ? item?.ToString()?.Trim() ?? string.Empty : string.Empty));
            }

            if (value is IEnumerable enumerable && value is not string)
            {
                return string.Concat(enumerable.Cast<object?>()
                    .Select(item => item?.ToString()?.Trim() ?? string.Empty));
            }

            return value;
        }

        private static object? FormatDataSelectValue(object? value)
        {
            var values = new List<string>();
            foreach (var item in EnumerateItemsOrSingle(value))
            {
                var dict = item.AsDictionary();
                if (dict == null)
                {
                    if (item != null) values.Add(item.ToString()!);
                    continue;
                }

                var selectedData = dict.FirstOrDefault(x => string.Equals(x.Key, "data", StringComparison.OrdinalIgnoreCase)).Value;
                var selectedDataDict = selectedData.AsDictionary();
                if (selectedDataDict != null)
                {
                    var dataText = selectedDataDict
                        .Where(x => x.Value != null)
                        .Select(x => $"{x.Key}: {x.Value}")
                        .ToList();
                    if (dataText.Count > 0)
                    {
                        values.Add(string.Join("; ", dataText));
                    }
                    continue;
                }

                var label = GetDictionaryValue(dict, "label") ?? GetDictionaryValue(dict, "name");
                var itemValue = GetDictionaryValue(dict, "value") ?? string.Empty;
                var display = string.IsNullOrWhiteSpace(label) ? itemValue : $"{label}: {itemValue}";
                if (!string.IsNullOrWhiteSpace(display)) values.Add(display);
            }

            return values.Count == 0 ? null : string.Join("; ", values);
        }

        private static object? FormatFileValue(object? value)
        {
            var names = new List<string>();
            foreach (var item in EnumerateItemsOrSingle(value))
            {
                var dict = item.AsDictionary();
                var display = dict == null
                    ? item?.ToString()
                    : GetDictionaryValue(dict, "name") ?? GetDictionaryValue(dict, "fileName") ?? GetDictionaryValue(dict, "url");

                if (!string.IsNullOrWhiteSpace(display))
                {
                    names.Add(display);
                }
            }

            return names.Count == 0 ? null : string.Join(',', names);
        }

        private static string? GetDictionaryValue(IDictionary<string, object?> dict, string key)
        {
            var pair = dict.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
            return pair.Value?.ToString();
        }

        private static object? FormatTableFormValue(object? value, IList<FieldDef>? columns)
        {
            if (value == null || columns == null || columns.Count == 0)
            {
                return new List<Dictionary<string, object?>>();
            }

            var columnMap = columns
                .Where(x => !string.IsNullOrWhiteSpace(x.Field))
                .ToDictionary(x => x.Field, StringComparer.OrdinalIgnoreCase);

            var rows = new List<Dictionary<string, object?>>();
            foreach (var row in EnumerateItems(value))
            {
                var rowDict = row.AsDictionary();
                if (rowDict == null)
                {
                    continue;
                }

                var rowResultDict = new Dictionary<string, object?>();
                foreach (var item in rowDict)
                {
                    if (!columnMap.TryGetValue(item.Key, out var columnDef))
                    {
                        continue;
                    }

                    rowResultDict[item.Key] = FormatFieldValue(item.Value, columnDef);
                }

                rows.Add(rowResultDict);
            }

            return rows;
        }

        private static object? FormatDisplayTableFormValue(object? value, IList<FieldDef>? columns)
        {
            if (value == null || columns == null || columns.Count == 0)
            {
                return new List<Dictionary<string, object?>>();
            }

            var visibleColumns = columns
                .Where(x => !x.Hidden && !string.IsNullOrWhiteSpace(x.Field))
                .ToDictionary(x => x.Field, StringComparer.OrdinalIgnoreCase);

            var rows = new List<Dictionary<string, object?>>();
            foreach (var row in EnumerateItemsOrSingle(value))
            {
                var rowDict = row.AsDictionary();
                if (rowDict == null)
                {
                    continue;
                }

                var rowResultDict = new Dictionary<string, object?>();
                foreach (var item in rowDict)
                {
                    if (!visibleColumns.TryGetValue(item.Key, out var columnDef))
                    {
                        continue;
                    }

                    rowResultDict[item.Key] = FormatDisplayFieldValue(item.Value, columnDef);
                }

                rows.Add(rowResultDict);
            }

            return rows;
        }

        private static object? FormatLabelValue(object? value)
        {
            var labels = new List<string>();
            foreach (var item in EnumerateItemsOrSingle(value))
            {
                var dict = item.AsDictionary();
                if (dict != null && dict.TryGetValue("label", out var labelObj))
                {
                    var label = labelObj?.ToString();
                    if (!string.IsNullOrWhiteSpace(label))
                    {
                        labels.Add(label);
                    }
                }
                else if (item != null)
                {
                    labels.Add(item.ToString()!);
                }
            }

            return labels.Count switch
            {
                0 => null,
                1 => labels[0],
                _ => string.Join(',', labels),
            };
        }

        private static object? FormatTimestampValue(object value, string? format)
        {
            var timestamp = TryGetTimestamp(value);
            if (!timestamp.HasValue)
            {
                return value;
            }

            return FormatTimestamp(timestamp.Value, string.IsNullOrWhiteSpace(format) ? Constants.Defaut_DateFormat : format);
        }

        private static object? FormatNumberValue(object value, string? format)
        {
            if (string.IsNullOrWhiteSpace(format))
            {
                return value;
            }

            var number = TryGetDecimal(value);
            if (!number.HasValue)
            {
                return value;
            }

            return number.Value.ToString(format, CultureInfo.InvariantCulture);
        }

        private static string FormatTimestamp(long timestamp, string format)
        {
            var shanghaiTime = TimeZoneInfo.ConvertTimeFromUtc(timestamp.ToDateTimeMs(), GetShanghaiTimeZone());
            return shanghaiTime.ToString(format, CultureInfo.InvariantCulture);
        }

        private static TimeZoneInfo GetShanghaiTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
            }
        }

        private static long? TryGetTimestamp(object? value)
        {
            if (value == null)
            {
                return null;
            }

            return value switch
            {
                JsonElement jsonElement => TryGetTimestampFromJsonElement(jsonElement),
                string s when long.TryParse(s.ToString(), out var parsed) => parsed,
                _ => Convert.ToInt64(value),
            };
        }

        private static decimal? TryGetDecimal(object? value)
        {
            if (value == null)
            {
                return null;
            }

            return value switch
            {
                JsonElement jsonElement => TryGetDecimalFromJsonElement(jsonElement),
                string s when decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => Convert.ToDecimal(value),
            };
        }

        private static long? TryGetTimestampFromJsonElement(JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                JsonValueKind.Number when jsonElement.TryGetInt64(out var timestamp) => timestamp,
                JsonValueKind.String when long.TryParse(jsonElement.GetString(), out var timestamp) => timestamp,
                _ => null,
            };
        }

        private static decimal? TryGetDecimalFromJsonElement(JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                JsonValueKind.Number when jsonElement.TryGetDecimal(out var number) => number,
                JsonValueKind.String when decimal.TryParse(jsonElement.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var number) => number,
                _ => null,
            };
        }

        private static IEnumerable<object?> EnumerateItems(object value)
        {
            if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in jsonElement.EnumerateArray())
                {
                    yield return item;
                }

                yield break;
            }

            if (value is IEnumerable enumerable and not string)
            {
                foreach (var item in enumerable)
                {
                    yield return item;
                }
            }
        }

        private static IEnumerable<object?> EnumerateItemsOrSingle(object? value)
        {
            if (value == null)
            {
                yield break;
            }

            if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in jsonElement.EnumerateArray())
                {
                    yield return item;
                }

                yield break;
            }

            if (value is IEnumerable enumerable and not string)
            {
                foreach (var item in enumerable)
                {
                    yield return item;
                }

                yield break;
            }

            yield return value;
        }

    }
}
