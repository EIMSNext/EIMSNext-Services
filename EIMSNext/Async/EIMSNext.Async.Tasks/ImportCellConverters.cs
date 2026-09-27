using System.Collections;
using System.Globalization;
using System.Text.Json;
using EIMSNext.Entities;
using NPOI.SS.UserModel;

namespace EIMSNext.Async.Tasks
{
    /// <summary>
    /// 表单数据导入的单元格转换工具集。
    /// 从 <see cref="Consumers.DataImportConsumer"/> 抽出，便于单测。
    /// </summary>
    internal static class ImportCellConverters
    {
        public static object ConvertNumber(ICell? cell, string text)
        {
            if (cell?.CellType == CellType.Numeric)
            {
                return cell.NumericCellValue;
            }

            return double.TryParse(text, out var value)
                ? value
                : throw new FormatException("数字格式无效");
        }

        public static object ConvertTimestamp(ICell? cell, string text)
        {
            DateTime date;
            if (cell?.CellType == CellType.Numeric)
            {
                date = DateUtil.IsCellDateFormatted(cell)
                    ? cell.DateCellValue ?? DateTime.FromOADate(cell.NumericCellValue)
                    : DateTime.FromOADate(cell.NumericCellValue);
            }
            else if (!DateTime.TryParse(text, out date))
            {
                throw new FormatException("日期格式无效");
            }

            return new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Local)).ToUnixTimeMilliseconds();
        }

        public static object ConvertEditableNumber(object raw)
        {
            return raw switch
            {
                byte value => value,
                short value => value,
                int value => value,
                long value => value,
                float value => value,
                double value => value,
                decimal value => value,
                _ => double.TryParse(ToCellText(raw), NumberStyles.Any, CultureInfo.InvariantCulture, out var invariantValue)
                    ? invariantValue
                    : double.TryParse(ToCellText(raw), out var localValue)
                        ? localValue
                        : throw new FormatException("数字格式无效"),
            };
        }

        public static object ConvertEditableTimestamp(object raw)
        {
            if (raw is long longValue)
            {
                return longValue;
            }
            if (raw is int intValue)
            {
                return intValue;
            }
            if (raw is double doubleValue)
            {
                return doubleValue > 10000000000 ? (long)doubleValue : new DateTimeOffset(DateTime.FromOADate(doubleValue)).ToUnixTimeMilliseconds();
            }

            var text = ToCellText(raw);
            if (long.TryParse(text, out var timestamp))
            {
                return timestamp;
            }
            if (!DateTime.TryParse(text, out var date))
            {
                throw new FormatException("日期格式无效");
            }

            return new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Local)).ToUnixTimeMilliseconds();
        }

        public static object ConvertSingleOption(string text, FieldDef field)
        {
            var option = field.Props.Options?.FirstOrDefault(x =>
                string.Equals(x.Value, text, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Label, text, StringComparison.OrdinalIgnoreCase));
            if (option == null && field.Props.Options?.Count > 0)
            {
                throw new FormatException($"选项不存在：{text}");
            }

            return option?.Value ?? text;
        }

        public static object ConvertEditableMultiOption(object raw, FieldDef field)
        {
            if (raw is IEnumerable enumerable && raw is not string)
            {
                var parts = enumerable
                    .Cast<object?>()
                    .Select(ToCellText)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();
                return ConvertMultiOption(string.Join(",", parts), field);
            }

            return ConvertMultiOption(ToCellText(raw), field);
        }

        public static object ConvertMultiOption(string text, FieldDef field)
        {
            var parts = SplitMultiValue(text);
            if (field.Props.Options == null || field.Props.Options.Count == 0)
            {
                return parts;
            }

            return parts.Select(item =>
            {
                var option = field.Props.Options.FirstOrDefault(x =>
                    string.Equals(x.Value, item, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x.Label, item, StringComparison.OrdinalIgnoreCase));
                return option?.Value ?? throw new FormatException($"选项不存在：{item}");
            }).ToList();
        }

        public static object ConvertUrlList(string text)
        {
            var parts = SplitMultiValue(text);
            return parts.Count <= 1 ? parts.FirstOrDefault() ?? string.Empty : parts;
        }

        public static object ConvertEditableUrlList(object raw)
        {
            if (raw is IEnumerable enumerable && raw is not string)
            {
                var parts = enumerable
                    .Cast<object?>()
                    .Select(ToCellText)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();
                return parts.Count <= 1 ? parts.FirstOrDefault() ?? string.Empty : parts;
            }

            return ConvertUrlList(ToCellText(raw));
        }

        public static object ConvertTextOrJson(string text)
        {
            if ((text.StartsWith('{') && text.EndsWith('}')) || (text.StartsWith('[') && text.EndsWith(']')))
            {
                try
                {
                    return text.DeserializeFromJson<object>() ?? text;
                }
                catch
                {
                    return text;
                }
            }

            return text;
        }

        public static List<string> SplitMultiValue(string text)
        {
            return text
                .Split([',', '，', ';', '；', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
        }

        public static string GetCellText(ICell? cell)
        {
            if (cell == null)
            {
                return string.Empty;
            }

            return cell.CellType switch
            {
                CellType.String => cell.StringCellValue?.Trim() ?? string.Empty,
                CellType.Numeric => DateUtil.IsCellDateFormatted(cell)
                    ? cell.DateCellValue?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty
                    : cell.NumericCellValue.ToString("G15"),
                CellType.Boolean => cell.BooleanCellValue ? "true" : "false",
                CellType.Formula => cell.ToString()?.Trim() ?? string.Empty,
                _ => cell.ToString()?.Trim() ?? string.Empty,
            };
        }

        public static bool IsRowEmpty(IRow? row)
        {
            if (row == null)
            {
                return true;
            }

            for (var i = row.FirstCellNum; i < row.LastCellNum; i++)
            {
                if (!string.IsNullOrWhiteSpace(GetCellText(row.GetCell(i))))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IsEmpty(object? value)
        {
            value = UnwrapJsonValue(value);
            if (value == null)
            {
                return true;
            }
            if (value is string s)
            {
                return string.IsNullOrWhiteSpace(s);
            }
            if (value is IEnumerable enumerable)
            {
                return !enumerable.Cast<object?>().Any(item => !IsEmpty(item));
            }

            return false;
        }

        public static string ToCellText(object? value)
        {
            value = UnwrapJsonValue(value);
            return value switch
            {
                null => string.Empty,
                DateTime date => date.ToString("yyyy-MM-dd HH:mm:ss"),
                IEnumerable enumerable when value is not string => string.Join(",", enumerable.Cast<object?>().Select(ToCellText)),
                _ => value.ToString()?.Trim() ?? string.Empty,
            };
        }

        public static object? UnwrapJsonValue(object? value)
        {
            if (value is not JsonElement element)
            {
                return value;
            }

            return element.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt64(out var longValue)
                    ? longValue
                    : element.TryGetDouble(out var doubleValue)
                        ? doubleValue
                        : element.ToString(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Array => element.EnumerateArray().Select(item => UnwrapJsonValue(item)).ToList(),
                JsonValueKind.Object => ToExpandoObject(element),
                _ => element.ToString(),
            };
        }

        public static global::System.Dynamic.ExpandoObject ToExpandoObject(JsonElement element)
        {
            var expando = new global::System.Dynamic.ExpandoObject();
            var dict = (IDictionary<string, object?>)expando;
            foreach (var prop in element.EnumerateObject())
            {
                dict[prop.Name] = UnwrapJsonValue(prop.Value);
            }

            return expando;
        }
    }
}
