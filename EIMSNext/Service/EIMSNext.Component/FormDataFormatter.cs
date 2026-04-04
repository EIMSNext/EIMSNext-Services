using System.Collections;
using System.Dynamic;
using System.Globalization;
using System.Text.Json;
using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Service.Entities;

namespace EIMSNext.Component
{
    public static class FormDataFormatter
    {
        public static ExpandoObject Format(FormData data, IList<FieldDef> fieldDefs)
        {
            var result = new ExpandoObject();
            var resultDict = (IDictionary<string, object?>)result;

            resultDict[Fields.CreateBy] = data.CreateBy;
            resultDict[Fields.CreateTime] = FormatTimestamp(data.CreateTime, Constants.Defaut_DateFormat);
            resultDict[Fields.UpdateBy] = data.UpdateBy;
            resultDict[Fields.UpdateTime] = data.UpdateTime.HasValue
                ? FormatTimestamp(data.UpdateTime.Value, Constants.Defaut_DateFormat)
                : string.Empty;

            var fieldMap = fieldDefs
                .Where(x => !string.IsNullOrWhiteSpace(x.Field))
                .ToDictionary(x => x.Field, StringComparer.OrdinalIgnoreCase);

            var dataDict = (IDictionary<string, object?>)data.Data;
            foreach (var item in dataDict)
            {
                if (!fieldMap.TryGetValue(item.Key, out var fieldDef))
                {
                    continue;
                }

                resultDict[item.Key] = FormatFieldValue(item.Value, fieldDef);
            }

            return result;
        }

        private static object? FormatFieldValue(object? value, FieldDef fieldDef)
        {
            if (value == null)
            {
                return null;
            }

            return fieldDef.Type.ToLowerInvariant() switch
            {
                FieldType.TimeStamp => FormatTimestampValue(value, fieldDef.Props.Format),
                FieldType.Number => FormatNumberValue(value, fieldDef.Props.Format),
                FieldType.TableForm => FormatTableFormValue(value, fieldDef.Columns),
                _ => value,
            };
        }

        private static object? FormatTableFormValue(object? value, IList<FieldDef>? columns)
        {
            if (value == null)
            {
                return value;
            }

            if (columns == null || columns.Count == 0)
            {
                return new List<ExpandoObject>();
            }

            var columnMap = columns
                .Where(x => !string.IsNullOrWhiteSpace(x.Field))
                .ToDictionary(x => x.Field, StringComparer.OrdinalIgnoreCase);

            var rows = new List<ExpandoObject>();
            foreach (var row in EnumerateItems(value))
            {
                var rowDict = AsDictionary(row);
                if (rowDict == null)
                {
                    continue;
                }

                var rowResult = new ExpandoObject();
                var rowResultDict = (IDictionary<string, object?>)rowResult;
                foreach (var item in rowDict)
                {
                    if (!columnMap.TryGetValue(item.Key, out var columnDef))
                    {
                        continue;
                    }

                    rowResultDict[item.Key] = FormatFieldValue(item.Value, columnDef);
                }

                rows.Add(rowResult);
            }

            return rows;
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
                long l => l,
                int i => i,
                double d when d >= long.MinValue && d <= long.MaxValue => Convert.ToInt64(d),
                decimal m when m >= long.MinValue && m <= long.MaxValue => Convert.ToInt64(m),
                JsonElement jsonElement => TryGetTimestampFromJsonElement(jsonElement),
                string s when long.TryParse(s, out var parsed) => parsed,
                _ => null,
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
                byte b => b,
                short s => s,
                int i => i,
                long l => l,
                float f => Convert.ToDecimal(f),
                double d => Convert.ToDecimal(d),
                decimal m => m,
                JsonElement jsonElement => TryGetDecimalFromJsonElement(jsonElement),
                string s when decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => null,
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

        private static IDictionary<string, object?>? AsDictionary(object? value)
        {
            if (value is ExpandoObject expandoObject)
            {
                return (IDictionary<string, object?>)expandoObject;
            }

            if (value is IDictionary<string, object?> dict)
            {
                return dict;
            }

            if (value is IDictionary<string, object> objectDict)
            {
                return objectDict.ToDictionary(x => x.Key, x => (object?)x.Value);
            }

            if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
            {
                var expando = new ExpandoObject();
                var expandoDict = (IDictionary<string, object?>)expando;
                foreach (var property in jsonElement.EnumerateObject())
                {
                    expandoDict[property.Name] = property.Value;
                }

                return expandoDict;
            }

            return null;
        }
    }
}
