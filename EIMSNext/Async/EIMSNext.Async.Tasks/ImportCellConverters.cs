using System.Collections;
using System.Globalization;
using System.Text.Json;
using EIMSNext.Entities;
using EIMSNext.Json.Serialization;
using NPOI.SS.UserModel;

namespace EIMSNext.Async.Tasks
{
    /// <summary>
    /// 表单数据导入的单元格转换工具集。
    /// 从 <see cref="Consumers.DataImportConsumer"/> 抽出，便于单测。
    /// </summary>
    internal static class ImportCellConverters
    {
        /// <summary>
        /// 数值单元格 → 动态字段值。
        /// </summary>
        /// <remarks>
        /// Excel 单元格值是 <see cref="double"/>，但 FormData.Data 的数值契约是
        /// 「整数 long、小数 decimal」（见 <see cref="DynamicValueReader"/>）。
        /// 这里必须归一化，否则导入的 12 存成 <c>12.0d</c>、DB 读回是 <c>12L</c>，
        /// 变更日志用 <c>Equals</c> 比较会判出一条不存在的修改。
        /// </remarks>
        public static object ConvertNumber(ICell? cell, string text)
        {
            var value = cell?.CellType == CellType.Numeric
                ? cell.NumericCellValue
                : double.TryParse(text, out var parsed)
                    ? parsed
                    : throw new FormatException("数字格式无效");

            return DynamicValueReader.NormalizeNumber(value);
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

        /// <summary>
        /// 可编辑数值 → 动态字段值（同样归一化到 long / decimal，理由见 <see cref="ConvertNumber"/>）。
        /// </summary>
        public static object ConvertEditableNumber(object raw)
        {
            return raw switch
            {
                byte or sbyte or short or ushort or int or uint or long or float or double or decimal
                    => DynamicValueReader.NormalizeNumber(raw)!,
                _ => double.TryParse(ToCellText(raw), NumberStyles.Any, CultureInfo.InvariantCulture, out var invariantValue)
                    ? DynamicValueReader.NormalizeNumber(invariantValue)
                    : double.TryParse(ToCellText(raw), out var localValue)
                        ? DynamicValueReader.NormalizeNumber(localValue)
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

        /// <summary>
        /// 把 <see cref="JsonElement"/> 还原为 CLR 类型（非 JsonElement 原样返回）。
        /// </summary>
        /// <remarks>
        /// 规则统一委托给 EIMSNext.Json 层的 <see cref="DynamicValueReader"/>：
        /// 导入写入 FormData.Data 的值必须与 DB 读回、API 请求路径的类型完全一致
        /// （整数→long、小数→decimal），否则变更日志用 <c>Equals</c> 比较会判出不存在的修改。
        /// </remarks>
        public static object? UnwrapJsonValue(object? value)
        {
            return value is JsonElement element ? DynamicValueReader.FromJsonElement(element) : value;
        }

        public static Dictionary<string, object?> ToDictionary(JsonElement element)
        {
            return DynamicValueReader.ToDictionary(element);
        }
    }
}
