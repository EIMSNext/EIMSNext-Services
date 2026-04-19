using System.Composition;
using System.Dynamic;
using System.Text.Json;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.Common;
using EIMSNext.Component;
using EIMSNext.Core;
using EIMSNext.Core.Query;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;
using MongoDB.Driver;

namespace EIMSNext.Async.Tasks.Export
{
    [Export(typeof(IExportProcessor))]
    [ExportMetadata(MefMetadata.Id, ExportProcessorIds.FormData)]
    public class FormDataExportProcessor : ExportProcessorBase
    {
        public override async Task<ExportFileBuilder.ExportFileResult> ExportAsync(
            ExportLog exportLog,
            IResolver resolver,
            CancellationToken ct)
        {
            var columns = exportLog.ColumnsJson?.DeserializeFromJson<List<ExportColumn>>() ?? [];
            var request = exportLog.FilterJson?.DeserializeFromJson<FormDataExportRequest>() ?? new FormDataExportRequest();
            var formDef = resolver.Resolve<IFormDefService>().Get(request.FormId)
                ?? throw new InvalidOperationException("表单不存在或已被删除");
            var fields = formDef.Content?.Items?.Where(x => !x.Hidden).ToList() ?? [];
            var filter = request.Filter?.ToFilterDefinition<FormData>() ?? Builders<FormData>.Filter.Empty;
            var fileNamePrefix = SanitizeFileName(formDef.Name);

            var result = await (exportLog.ActualFormat == ExportFormat.Excel
                ? ExportExcelByBatchAsync<FormData>(
                    $"{fileNamePrefix}-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx",
                    columns,
                    filter,
                    resolver,
                    ct,
                    1000,
                    (sheet, styles, exportColumns, rows, rowIndex) => WriteExcelRows(sheet, styles, exportColumns, fields, rows, rowIndex))
                : ExportCsvByBatchAsync<FormData>(
                    $"{fileNamePrefix}-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
                    columns,
                    filter,
                    resolver,
                    ct,
                    1000,
                    (writer, exportColumns, rows) => WriteCsvRows(writer, exportColumns, fields, rows)));

            result.FormName = formDef.Name;
            return result;
        }

        private sealed class FormDataColumnBinding
        {
            public required ExportColumn Column { get; init; }

            public FieldDef? Field { get; init; }

            public FieldDef? ParentField { get; init; }

            public bool IsSystemField { get; init; }

            public bool IsSubField => ParentField != null;

            public string Key => Column.Key;
        }

        private sealed class FlattenedFormDataRow
        {
            public Dictionary<string, object?> Values { get; init; } = new(StringComparer.OrdinalIgnoreCase);

            public bool IsPlaceholder { get; init; }
        }

        internal static void WriteCsvRows(HKH.CSV.CSVWriter writer, List<ExportColumn> columns, List<FieldDef> fields, IEnumerable<FormData> rows)
        {
            var bindings = BuildColumnBindings(columns, fields);
            foreach (var item in rows)
            {
                foreach (var row in FlattenRows(item, bindings))
                {
                    writer.Write(columns.Select(column => ExportFileBuilder.FormatCsvCell(GetCellValue(bindings, column, row))), false);
                }
            }
        }

        internal static int WriteExcelRows(NPOI.SS.UserModel.ISheet sheet, ExportFileBuilder.ExcelStyles styles, List<ExportColumn> columns, List<FieldDef> fields, IEnumerable<FormData> rows, int startRowIndex)
        {
            var bindings = BuildColumnBindings(columns, fields);
            var rowIndex = startRowIndex;
            var mergeColumnIndexes = columns
                .Select((column, index) => new { column, index })
                .Where(x => !bindings.TryGetValue(x.column.Key, out var binding) || !binding.IsSubField)
                .Select(x => x.index)
                .ToList();

            foreach (var item in rows)
            {
                var flattenedRows = FlattenRows(item, bindings);
                var start = rowIndex;
                foreach (var flatRow in flattenedRows)
                {
                    var row = sheet.CreateRow(rowIndex++);
                    for (var colIndex = 0; colIndex < columns.Count; colIndex++)
                    {
                        ExportFileBuilder.WriteExcelCell(row.CreateCell(colIndex), GetCellValue(bindings, columns[colIndex], flatRow), styles);
                    }
                }

                if (flattenedRows.Count > 1)
                {
                    foreach (var colIndex in mergeColumnIndexes)
                    {
                        sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(start, rowIndex - 1, colIndex, colIndex));
                    }
                }
            }

            return rowIndex;
        }

        private static Dictionary<string, FormDataColumnBinding> BuildColumnBindings(List<ExportColumn> columns, List<FieldDef> fields)
        {
            var result = new Dictionary<string, FormDataColumnBinding>(StringComparer.OrdinalIgnoreCase);
            var fieldMap = fields.Where(x => !string.IsNullOrWhiteSpace(x.Field)).ToDictionary(x => x.Field, StringComparer.OrdinalIgnoreCase);

            foreach (var column in columns)
            {
                FieldDef? field = null;
                FieldDef? parentField = null;

                if (column.Key.Contains('>'))
                {
                    var parts = column.Key.Split('>', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2 && fieldMap.TryGetValue(parts[0], out parentField) && parentField.Columns != null)
                    {
                        field = parentField.Columns.FirstOrDefault(x => string.Equals(x.Field, parts[1], StringComparison.OrdinalIgnoreCase));
                    }
                }
                else
                {
                    fieldMap.TryGetValue(column.Key, out field);
                }

                result[column.Key] = new FormDataColumnBinding
                {
                    Column = column,
                    Field = field,
                    ParentField = parentField,
                    IsSystemField = Fields.IsSystemField(column.Key),
                };
            }

            return result;
        }

        private static List<FlattenedFormDataRow> FlattenRows(FormData data, Dictionary<string, FormDataColumnBinding> bindings)
        {
            var formatted = (IDictionary<string, object?>)FormDataFormatter.FormatForDisplay(data, ResolveTopLevelFields(bindings));
            var masterValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            var subGroupRows = new Dictionary<string, List<IDictionary<string, object?>>>(StringComparer.OrdinalIgnoreCase);

            foreach (var binding in bindings.Values)
            {
                if (binding.IsSubField)
                {
                    var parentKey = binding.ParentField!.Field;
                    if (!subGroupRows.ContainsKey(parentKey))
                    {
                        subGroupRows[parentKey] = ResolveSubRows(formatted, binding.ParentField);
                    }
                }
                else
                {
                    masterValues[binding.Key] = ResolveMasterValue(data, formatted, binding);
                }
            }

            if (subGroupRows.Count == 0)
            {
                return [new FlattenedFormDataRow { Values = masterValues }];
            }

            var maxRowCount = Math.Max(1, subGroupRows.Values.Max(x => x.Count));
            var rows = new List<FlattenedFormDataRow>(maxRowCount);
            for (var rowIndex = 0; rowIndex < maxRowCount; rowIndex++)
            {
                var values = new Dictionary<string, object?>(masterValues, StringComparer.OrdinalIgnoreCase);
                var hasSubValue = false;

                foreach (var binding in bindings.Values.Where(x => x.IsSubField))
                {
                    var parentField = binding.ParentField!.Field;
                    var subRows = subGroupRows[parentField];
                    var subRow = rowIndex < subRows.Count ? subRows[rowIndex] : null;
                    var subField = binding.Field?.Field ?? binding.Key.Split('>', 2)[1];
                    var value = subRow != null ? ResolveValue(subRow, subField) : null;
                    values[binding.Key] = value;
                    hasSubValue |= !IsNullOrEmptyValue(value);
                }

                rows.Add(new FlattenedFormDataRow
                {
                    Values = values,
                    IsPlaceholder = !hasSubValue,
                });
            }

            return rows;
        }

        private static ExportFileBuilder.ExportCellValue GetCellValue(Dictionary<string, FormDataColumnBinding> bindings, ExportColumn column, FlattenedFormDataRow row)
        {
            row.Values.TryGetValue(column.Key, out var value);

            if (!bindings.TryGetValue(column.Key, out var binding))
            {
                return CreateCellValue(value, column.Type);
            }

            if (binding.IsSubField && row.IsPlaceholder)
            {
                return new ExportFileBuilder.ExportCellValue { Type = column.Type, Text = string.Empty };
            }

            return CreateCellValue(value, column.Type);
        }

        private static List<FieldDef> ResolveTopLevelFields(Dictionary<string, FormDataColumnBinding> bindings)
        {
            return bindings.Values
                .Where(x => !x.IsSystemField)
                .Select(x => x.IsSubField ? x.ParentField : x.Field)
                .Where(x => x != null)
                .GroupBy(x => x!.Field, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First()!)
                .ToList();
        }

        private static List<IDictionary<string, object?>> ResolveSubRows(IDictionary<string, object?> formatted, FieldDef parentField)
        {
            if (!formatted.TryGetValue(parentField.Field, out var value) || value == null)
            {
                return [];
            }

            var rows = new List<IDictionary<string, object?>>();
            foreach (var item in EnumerateItems(value))
            {
                var dict = AsDictionary(item);
                if (dict != null)
                {
                    rows.Add(dict);
                }
            }

            return rows;
        }

        private static object? ResolveValue(IDictionary<string, object?> dict, string key)
        {
            return dict.TryGetValue(key, out var value) ? value : null;
        }

        private static object? ResolveMasterValue(FormData data, IDictionary<string, object?> formatted, FormDataColumnBinding binding)
        {
            if (!binding.IsSystemField)
            {
                return ResolveValue(formatted, binding.Key);
            }

            return binding.Key switch
            {
                "id" or "_id" => data.Id,
                "createBy" => data.CreateBy?.Label ?? string.Empty,
                "createTime" => ExportFileBuilder.ToLocalDateTime(data.CreateTime),
                "updateBy" => data.UpdateBy?.Label ?? string.Empty,
                "updateTime" => ExportFileBuilder.ToLocalDateTime(data.UpdateTime),
                "flowStatus" => data.FlowStatus.ToString(),
                _ => ResolveValue(formatted, binding.Key),
            };
        }

        private static ExportFileBuilder.ExportCellValue CreateCellValue(object? value, ExportColumnType type)
        {
            if (value == null)
            {
                return new ExportFileBuilder.ExportCellValue { Type = type, Text = string.Empty };
            }

            if (type == ExportColumnType.Date)
            {
                if (value is DateTime dateTime)
                {
                    return new ExportFileBuilder.ExportCellValue { Type = type, DateTime = dateTime };
                }

                if (DateTime.TryParse(value.ToString(), out var parsedDateTime))
                {
                    return new ExportFileBuilder.ExportCellValue { Type = type, DateTime = parsedDateTime };
                }
            }

            if (type == ExportColumnType.Number)
            {
                if (value is decimal decimalValue)
                {
                    return new ExportFileBuilder.ExportCellValue { Type = type, Number = decimalValue };
                }

                if (decimal.TryParse(value.ToString(), out var parsedDecimal))
                {
                    return new ExportFileBuilder.ExportCellValue { Type = type, Number = parsedDecimal };
                }
            }

            return new ExportFileBuilder.ExportCellValue
            {
                Type = type,
                Text = ExportFileBuilder.SanitizeForExcel(value.ToString() ?? string.Empty),
            };
        }

        private static bool IsNullOrEmptyValue(object? value)
        {
            return value == null || string.IsNullOrWhiteSpace(value.ToString());
        }

        private static IEnumerable<object?> EnumerateItems(object value)
        {
            if (value is System.Collections.IEnumerable enumerable and not string)
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

            return null;
        }
    }
}
