using System.Collections;
using System.Dynamic;
using System.Text.Json;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Async.RabbitMQ.Messaging;
using EIMSNext.Async.Tasks.SystemTask;
using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Core;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Query;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using EIMSNext.Storage.Abstractions;
using HKH.Mef2.Integration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace EIMSNext.Async.Tasks.Consumers
{
    public class DataImportConsumer : TaskConsumerBase<DataImportTaskArgs, DataImportConsumer>
    {
        public DataImportConsumer(IServiceScopeFactory scopeFactory)
            : base(scopeFactory)
        {
        }

        private static readonly TimeSpan ProcessingRequeueDelay = TimeSpan.FromSeconds(10);

        protected override async Task HandleAsync(DataImportTaskArgs args, CancellationToken ct, IResolver resolver)
        {
            var importLogService = resolver.Resolve<IFormDataImportLogService>();
            var importLog = importLogService.Get(args.ImportLogId);
            if (importLog == null || importLog.RetryCount != args.RetryCount)
            {
                return;
            }

            if (importLog.Status == FormDataImportStatus.Processing && !IsProcessingExpired(importLog))
            {
                throw new TaskRequeueException("Import task is already processing.", ProcessingRequeueDelay);
            }

            if (importLog.Status == FormDataImportStatus.Processing)
            {
                var message = "导入任务处理超时，请重新发起导入";
                await importLogService.MarkFailedAsync(importLog.Id, message);
                importLog.ErrorMessage = message;
                await PublishMessageAsync(importLog, false, message, resolver, ct);
                return;
            }

            if (importLog.Status != FormDataImportStatus.Pending &&
                importLog.Status != FormDataImportStatus.Processing)
            {
                return;
            }

            FormDataImportProcessor? processor = null;
            try
            {
                if (!await importLogService.TryMarkProcessingAsync(importLog.Id, args.RetryCount))
                {
                    throw new TaskRequeueException("Import task state was not acquired.", ProcessingRequeueDelay);
                }

                await PrepareServiceContextAsync(importLog, resolver, ct);
                processor = new FormDataImportProcessor(resolver, importLog, Logger, ct);
                await processor.ExecuteAsync();
            }
            catch (TaskRequeueException)
            {
                throw;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                importLog.ErrorMessage = ex.Message;
                var partial = processor?.Result ?? new ImportRunResult();
                try
                {
                    var storage = resolver.Resolve<IStorageProvider>();
                    var report = BuildTaskFailureReport(importLog, ex);
                    var reportObjectKey = $"Import\\{importLog.CorpId}\\{DateTime.UtcNow:yyyyMMdd}\\{importLog.Id}_task_error.xlsx";
                    if (storage.Upload(report.Content, reportObjectKey))
                    {
                        var reportUrl = $"{storage.Setting.BaseUrl.TrimEnd('/')}/{reportObjectKey.TrimStart('/', '\\').Replace("\\", "/")}";
                        await importLogService.MarkFailedAsync(args.ImportLogId, ex.Message, report.FileName, reportObjectKey, reportUrl);
                        importLog.ErrorReportFileName = report.FileName;
                        importLog.ErrorReportObjectKey = reportObjectKey;
                        importLog.ErrorReportDownloadUrl = reportUrl;
                    }
                    else
                    {
                        await importLogService.MarkFailedAsync(args.ImportLogId, ex.Message);
                    }
                }
                catch (Exception reportEx)
                {
                    Logger.LogWarning(reportEx, "Create import task failure report failed. ImportLogId={ImportLogId}", args.ImportLogId);
                    await importLogService.MarkFailedAsync(args.ImportLogId, ex.Message);
                }

                await PublishMessageAsync(importLog, false, BuildTaskFailureDetail(partial, ex), resolver, ct);
            }
        }

        private static bool IsProcessingExpired(FormDataImportLog importLog)
        {
            return !importLog.ProcessingExpireTime.HasValue ||
                importLog.ProcessingExpireTime.Value <= DateTime.UtcNow.ToTimeStampMs();
        }

        private static async Task PrepareServiceContextAsync(FormDataImportLog importLog, IResolver resolver, CancellationToken ct)
        {
            var serviceContext = resolver.GetServiceContext();
            serviceContext.CorpId = importLog.CorpId ?? string.Empty;
            serviceContext.Operator = importLog.CreateBy;
            serviceContext.Action = importLog.ImportAction;

            if (importLog.ImportAction == DataAction.Submit)
            {
                var tokenProvider = resolver.Resolve<ISystemTaskTokenProvider>();
                serviceContext.AccessToken = await tokenProvider.GetAccessTokenAsync(
                    importLog.CorpId ?? string.Empty,
                    "form-import",
                    importLog.Id,
                    ct);
            }
        }

        private static async Task PublishMessageAsync(FormDataImportLog importLog, bool success, string detail, IResolver resolver, CancellationToken ct)
        {
            var employee = ResolveOwner(resolver, importLog);
            if (employee == null)
            {
                return;
            }

            await resolver.Resolve<IMessagePublisher>().PublishAsync(new SystemMessageTaskArgs
            {
                CorpId = importLog.CorpId ?? string.Empty,
                NotifyId = importLog.Id,
                Title = success ? "数据导入完成" : "数据导入失败",
                Detail = detail,
                Url = success ? string.Empty : importLog.ErrorReportDownloadUrl ?? string.Empty,
                ExpireTime = DateTime.UtcNow.AddDays(30).ToTimeStampMs(),
                Category = MessageCategory.DataNotify,
                MessageType = MessageType.ImportNotify,
                Receivers =
                [
                    new NotifyReceiver
                    {
                        EmpId = employee.Id,
                        EmpName = employee.EmpName,
                        Email = employee.WorkEmail,
                    }
                ]
            }, ct);
        }

        private static Employee? ResolveOwner(IResolver resolver, FormDataImportLog importLog)
        {
            var empId = importLog.CreateBy?.Id;
            return string.IsNullOrWhiteSpace(empId)
                ? null
                : resolver.Resolve<IEmployeeService>().Get(empId);
        }

        internal static ErrorReport BuildTaskFailureReport(FormDataImportLog importLog, Exception ex)
        {
            var workbook = new XSSFWorkbook();
            var sheet = workbook.CreateSheet("导入失败");
            var header = sheet.CreateRow(0);
            header.CreateCell(0).SetCellValue("表单");
            header.CreateCell(1).SetCellValue("源文件");
            header.CreateCell(2).SetCellValue("失败原因");

            var row = sheet.CreateRow(1);
            row.CreateCell(0).SetCellValue(importLog.FormName ?? importLog.FormId);
            row.CreateCell(1).SetCellValue(importLog.SourceFileName ?? string.Empty);
            row.CreateCell(2).SetCellValue(ex.Message);

            using var ms = new MemoryStream();
            workbook.Write(ms, leaveOpen: true);
            return new ErrorReport
            {
                FileName = $"导入失败报告_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx",
                Content = ms.ToArray(),
            };
        }

        internal static string BuildTaskFailureDetail(ImportRunResult result, Exception ex)
        {
            return $"数据导入中断，已处理 {result.ProcessedCount} 条（新增 {result.AddCount} / 更新 {result.UpdateCount}），失败原因：{ex.Message}";
        }

        internal sealed class FormDataImportProcessor
        {
            private readonly IResolver _resolver;
            private readonly FormDataImportLog _importLog;
            private readonly Microsoft.Extensions.Logging.ILogger _logger;
            private readonly CancellationToken _ct;
            private readonly IFormDataImportLogService _importLogService;
            private readonly IFormDataService _formDataService;
            private readonly IFormDefService _formDefService;
            private readonly IStorageProvider _storage;
            private readonly List<FormDataImportMappingItem> _mappings;
            private readonly Dictionary<string, FieldDef> _fieldMap;
            private readonly bool _usesOrganizationField;

            /// <summary>
            /// 累计执行结果。当任务级异常抛出时，<see cref="DataImportConsumer.HandleAsync"/> 读取此属性拼接系统消息。
            /// </summary>
            public ImportRunResult Result { get; } = new();

            public FormDataImportProcessor(IResolver resolver, FormDataImportLog importLog, Microsoft.Extensions.Logging.ILogger logger, CancellationToken ct)
            {
                _resolver = resolver;
                _importLog = importLog;
                _logger = logger;
                _ct = ct;
                _importLogService = resolver.Resolve<IFormDataImportLogService>();
                _formDataService = resolver.Resolve<IFormDataService>();
                _formDefService = resolver.Resolve<IFormDefService>();
                _storage = resolver.Resolve<IStorageProvider>();
                _mappings = importLog.MappingJson.DeserializeFromJson<List<FormDataImportMappingItem>>() ?? [];
                _fieldMap = BuildFieldMap(importLog.FieldSnapshotJson, _formDefService.Get(importLog.FormId));
                _usesOrganizationField = ComputeUsesOrganizationField();
                var invalidMapping = _mappings.FirstOrDefault(mapping => !_fieldMap.ContainsKey(mapping.Field));
                if (invalidMapping != null)
                {
                    throw new InvalidOperationException($"字段不存在或不可导入：{invalidMapping.FieldTitle ?? invalidMapping.Field}");
                }
            }

            private bool ComputeUsesOrganizationField()
            {
                return _mappings.Any(mapping =>
                    _fieldMap.TryGetValue(mapping.Field, out var field) &&
                    field.Type is FieldType.Employee1 or FieldType.Employee2 or FieldType.Department1 or FieldType.Department2);
            }

            public async Task ExecuteAsync()
            {
                var rows = BuildRecordsFromWorkbook();

                await _importLogService.MarkProcessingAsync(_importLog.Id, rows.Count);
                EnforceRowLimit(rows.Count);

                try
                {
                    foreach (var row in rows)
                    {
                        _ct.ThrowIfCancellationRequested();
                        await ImportRecordAsync(row, Result);

                        if (Result.ProcessedCount % 20 == 0 || Result.ProcessedCount == rows.Count)
                        {
                            await _importLogService.UpdateProgressAsync(
                                _importLog.Id,
                                Result.ProcessedCount,
                                Result.AddCount,
                                Result.UpdateCount,
                                Result.ErrorRows.Count);
                        }
                    }
                }
                catch
                {
                    // 任务级异常：先刷一次当前 in-memory 计数到 Mongo，让前端能看到中断时的累计进度，再向上抛
                    if (Result.ProcessedCount > 0)
                    {
                        try
                        {
                            await _importLogService.UpdateProgressAsync(
                                _importLog.Id,
                                Result.ProcessedCount,
                                Result.AddCount,
                                Result.UpdateCount,
                                Result.ErrorRows.Count);
                        }
                        catch
                        {
                            // flush 失败不阻塞原异常
                        }
                    }
                    throw;
                }

                if (Result.ErrorRows.Count == 0)
                {
                    await _importLogService.MarkSucceededAsync(_importLog.Id, rows.Count, Result.AddCount, Result.UpdateCount);
                    await PublishMessageAsync(_importLog, true, BuildSuccessDetail(Result), _resolver, _ct);
                    return;
                }

                var report = BuildErrorReport(Result.ErrorRows);
                var reportObjectKey = $"Import\\{_importLog.CorpId}\\{DateTime.UtcNow:yyyyMMdd}\\{_importLog.Id}_errors.xlsx";
                if (!_storage.Upload(report.Content, reportObjectKey))
                {
                    throw new InvalidOperationException("上传导入错误报告失败");
                }

                var reportUrl = $"{_storage.Setting.BaseUrl.TrimEnd('/')}/{reportObjectKey.TrimStart('/', '\\').Replace("\\", "/")}";
                var editableRows = Result.ErrorRows.Count <= EIMSNext.Common.Constants.FormDataImportMaxEditableErrors
                    ? Result.ErrorRows.Select(x => x.ToEditableRow()).ToList()
                    : [];
                var editableJson = editableRows.Count > 0 ? editableRows.SerializeToJson() : null;

                await _importLogService.MarkCompletedWithErrorsAsync(
                    _importLog.Id,
                    rows.Count,
                    Result.AddCount,
                    Result.UpdateCount,
                    Result.ErrorRows.Count,
                    report.FileName,
                    reportObjectKey,
                    reportUrl,
                    editableJson,
                    null,
                    editableRows.Count);

                _importLog.ErrorReportDownloadUrl = reportUrl;
                await PublishMessageAsync(_importLog, false, BuildFailedDetail(Result), _resolver, _ct);
            }

            private async Task ImportRecordAsync(ImportRecord record, ImportRunResult result)
            {
                result.ProcessedCount++;

                try
                {
                    var errors = ValidateRecordShape(record);
                    if (errors.Count > 0)
                    {
                        result.ErrorRows.Add(record.WithErrors(errors));
                        return;
                    }

                    var action = ResolveRowAction(record, out var matched);
                    record.RowAction = action;
                    record.MatchedDataId = matched?.Id;

                    if (action == FormDataImportRowAction.Add)
                    {
                        var validationErrors = ValidateImportData(record.Data);
                        if (validationErrors.Count > 0)
                        {
                            result.ErrorRows.Add(record.WithErrors(validationErrors));
                            return;
                        }

                        var entity = new FormData
                        {
                            CorpId = _importLog.CorpId,
                            AppId = _importLog.AppId,
                            FormId = _importLog.FormId,
                            FlowStatus = FlowStatus.Draft,
                            Data = record.Data,
                        };
                        await _formDataService.AddAsync(entity);
                        result.AddCount++;
                    }
                    else
                    {
                        if (matched == null)
                        {
                            result.ErrorRows.Add(record.WithError(_importLog.MatchField, "未找到匹配数据"));
                            return;
                        }

                        var mergedData = CloneData(matched.Data);
                        MergeData(mergedData, record.Data);
                        var validationErrors = ValidateImportData(mergedData);
                        if (validationErrors.Count > 0)
                        {
                            result.ErrorRows.Add(record.WithErrors(validationErrors));
                            return;
                        }

                        MergeData(matched.Data, record.Data);
                        await _formDataService.ReplaceAsync(matched);
                        result.UpdateCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Import row failed. ImportLogId={ImportLogId}, Row={Row}", _importLog.Id, record.StartRowNumber);
                    result.ErrorRows.Add(record.WithError(null, ex.Message));
                }
            }

            private List<FormDataImportCellError> ValidateRecordShape(ImportRecord record)
            {
                var errors = new List<FormDataImportCellError>(record.Errors);
                if (_importLog.Mode != FormDataImportMode.AddOnly)
                {
                    var value = GetValue(record.Data, _importLog.MatchField);
                    if (ImportCellConverters.IsEmpty(value))
                    {
                        errors.Add(new FormDataImportCellError
                        {
                            Field = _importLog.MatchField,
                            FieldTitle = ResolveFieldTitle(_importLog.MatchField),
                            Message = "匹配字段不能为空",
                        });
                    }
                }

                return errors;
            }

            private List<FormDataImportCellError> ValidateImportData(ExpandoObject data)
            {
                if (!_importLog.TriggerValidation)
                {
                    return [];
                }

                var errors = new List<FormDataImportCellError>();
                foreach (var (fieldKey, field) in _fieldMap)
                {
                    if (!IsRequired(field))
                    {
                        continue;
                    }

                    if (!fieldKey.Contains('>'))
                    {
                        if (ImportCellConverters.IsEmpty(GetTopLevelValue(data, fieldKey)))
                        {
                            errors.Add(new FormDataImportCellError
                            {
                                Field = fieldKey,
                                FieldTitle = ResolveFieldTitle(fieldKey),
                                Message = "必填字段不能为空",
                            });
                        }

                        continue;
                    }

                    var parts = fieldKey.Split('>', 2, StringSplitOptions.RemoveEmptyEntries);
                    var rows = GetChildRows(data, parts[0]);
                    if (rows.Count == 0)
                    {
                        errors.Add(new FormDataImportCellError
                        {
                            Field = fieldKey,
                            FieldTitle = ResolveFieldTitle(fieldKey),
                            Message = "必填字段不能为空",
                        });
                        continue;
                    }

                    for (var index = 0; index < rows.Count; index++)
                    {
                        if (ImportCellConverters.IsEmpty(GetDictionaryValue(rows[index], parts[1])))
                        {
                            errors.Add(new FormDataImportCellError
                            {
                                Field = fieldKey,
                                FieldTitle = ResolveFieldTitle(fieldKey),
                                Message = $"第 {index + 1} 条明细不能为空",
                            });
                        }
                    }
                }

                return errors;
            }

            private FormDataImportRowAction ResolveRowAction(ImportRecord record, out FormData? matched)
            {
                matched = null;
                if (_importLog.Mode == FormDataImportMode.AddOnly)
                {
                    return FormDataImportRowAction.Add;
                }

                var matchValue = GetValue(record.Data, _importLog.MatchField);
                record.MatchValue = ImportCellConverters.ToCellText(matchValue);
                if (ImportCellConverters.IsEmpty(matchValue))
                {
                    return _importLog.Mode == FormDataImportMode.UpdateOnly
                        ? FormDataImportRowAction.Update
                        : FormDataImportRowAction.Add;
                }

                var found = FindMatchedData(_importLog.MatchField!, matchValue!).ToList();
                if (found.Count > 1)
                {
                    throw new InvalidOperationException($"匹配字段存在多条数据：{record.MatchValue}");
                }

                matched = found.FirstOrDefault();
                if (matched != null)
                {
                    return FormDataImportRowAction.Update;
                }

                if (_importLog.Mode == FormDataImportMode.UpdateOnly)
                {
                    throw new InvalidOperationException($"未找到匹配数据：{record.MatchValue}");
                }

                return FormDataImportRowAction.Add;
            }

            private IEnumerable<FormData> FindMatchedData(string field, object value)
            {
                var filter = new DynamicFilter
                {
                    Rel = FilterRel.And,
                    Items =
                    [
                        new DynamicFilter { Field = Fields.CorpId, Op = FilterOp.Eq, Value = _importLog.CorpId },
                        new DynamicFilter { Field = Fields.FormId, Op = FilterOp.Eq, Value = _importLog.FormId },
                        new DynamicFilter { Field = Fields.DeleteFlag, Op = FilterOp.Ne, Value = true },
                        new DynamicFilter { Field = $"{Fields.Data}.{field}", Op = FilterOp.Eq, Value = value },
                    ]
                };
                var dataScopeFilter = ReadDataScopeFilter();
                if (dataScopeFilter != null && !dataScopeFilter.IsEmpty)
                {
                    filter.Items!.Add(dataScopeFilter);
                }

                var found = _formDataService.Find(new DynamicFindOptions<FormData>
                {
                    Filter = filter,
                    Take = 2,
                });
                return ((IEnumerable<FormData>)found).ToList();
            }

            private DynamicFilter? ReadDataScopeFilter()
            {
                return string.IsNullOrWhiteSpace(_importLog.DataScopeFilterJson)
                    ? null
                    : _importLog.DataScopeFilterJson.DeserializeFromJson<DynamicFilter>();
            }

            private List<ImportRecord> BuildRecordsFromWorkbook()
            {
                using var stream = _storage.Download(_importLog.SourceObjectKey)
                    ?? throw new InvalidOperationException("导入源文件不存在");
                using var workbook = WorkbookFactory.Create(stream);
                var sheet = string.IsNullOrWhiteSpace(_importLog.SheetName)
                    ? workbook.GetSheetAt(0)
                    : workbook.GetSheet(_importLog.SheetName) ?? workbook.GetSheetAt(0);

                var headerRow = sheet.GetRow(Math.Max(0, _importLog.HeaderRowIndex - 1));
                if (headerRow == null)
                {
                    throw new InvalidOperationException("标题行不存在");
                }

                if (headerRow.LastCellNum > ImportMaxColumns)
                {
                    throw new InvalidOperationException("导入列数不能超过 500 列");
                }

                var records = new List<ImportRecord>();
                ImportRecord? current = null;
                for (var rowIndex = _importLog.HeaderRowIndex; rowIndex <= sheet.LastRowNum; rowIndex++)
                {
                    var row = sheet.GetRow(rowIndex);
                    if (ImportCellConverters.IsRowEmpty(row))
                    {
                        continue;
                    }

                    var rowValues = ReadMappedRow(row);
                    var hasTopLevel = rowValues.Any(x => !x.Key.Contains('>') && (!ImportCellConverters.IsEmpty(x.Value.Value) || x.Value.Error != null));
                    var hasChild = rowValues.Any(x => x.Key.Contains('>') && (!ImportCellConverters.IsEmpty(x.Value.Value) || x.Value.Error != null));
                    if (hasTopLevel || current == null)
                    {
                        current = new ImportRecord
                        {
                            RecordIndex = records.Count + 1,
                            StartRowNumber = rowIndex + 1,
                            Data = new ExpandoObject(),
                        };
                        records.Add(current);
                    }
                    else if (hasChild)
                    {
                        current.EndRowNumber = rowIndex + 1;
                    }

                    ApplyRowValues(current, rowValues);
                    EnforceRowLimit(records.Count);
                }

                return records;
            }

            private void EnforceRowLimit(int count)
            {
                if (count > MaxRows)
                {
                    throw new InvalidOperationException("导入数据不能超过 10 万行");
                }
                if (_importLog.TriggerWorkflow && count > 300)
                {
                    throw new InvalidOperationException("导入触发流程最多允许导入 300 条数据");
                }
                if (_usesOrganizationField && count > 10000)
                {
                    throw new InvalidOperationException("导入存在部门或成员字段时不能超过 1 万行");
                }
            }

            private const int MaxRows = 100000;
            private const int ImportMaxColumns = 500;

            private Dictionary<string, ImportCellValue> ReadMappedRow(IRow row)
            {
                var values = new Dictionary<string, ImportCellValue>(StringComparer.OrdinalIgnoreCase);
                foreach (var mapping in _mappings)
                {
                    if (!_fieldMap.TryGetValue(mapping.Field, out var field))
                    {
                        continue;
                    }

                    var cell = row.GetCell(mapping.ColumnIndex);
                    var value = ConvertCellValue(cell, field, out var error);
                    values[mapping.Field] = new ImportCellValue(value, error);
                }

                return values;
            }

            private void ApplyRowValues(ImportRecord record, Dictionary<string, ImportCellValue> rowValues)
            {
                var dict = (IDictionary<string, object?>)record.Data;
                var childRowByParent = new Dictionary<string, IDictionary<string, object?>>(StringComparer.OrdinalIgnoreCase);

                foreach (var (field, cellValue) in rowValues)
                {
                    if (cellValue.Error != null)
                    {
                        record.Errors.Add(new FormDataImportCellError
                        {
                            Field = field,
                            FieldTitle = ResolveFieldTitle(field),
                            Message = cellValue.Error,
                        });
                        continue;
                    }

                    if (field.Contains('>'))
                    {
                        var parts = field.Split('>', 2, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length != 2 || ImportCellConverters.IsEmpty(cellValue.Value))
                        {
                            continue;
                        }

                        if (!childRowByParent.TryGetValue(parts[0], out var childRow))
                        {
                            childRow = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                            childRowByParent[parts[0]] = childRow;
                        }
                        childRow[parts[1]] = cellValue.Value;
                    }
                    else if (!ImportCellConverters.IsEmpty(cellValue.Value))
                    {
                        dict[field] = cellValue.Value;
                    }
                }

                foreach (var (parent, childRow) in childRowByParent)
                {
                    if (childRow.Count == 0)
                    {
                        continue;
                    }

                    if (!dict.TryGetValue(parent, out var table) || table is not List<Dictionary<string, object?>> rows)
                    {
                        rows = [];
                        dict[parent] = rows;
                    }

                    rows.Add(new Dictionary<string, object?>(childRow, StringComparer.OrdinalIgnoreCase));
                }
            }

            private ExpandoObject NormalizeEditableData(ExpandoObject source, out List<FormDataImportCellError> errors)
            {
                errors = [];
                var result = new ExpandoObject();
                var resultDict = (IDictionary<string, object?>)result;
                var childRowsByParent = new Dictionary<string, List<Dictionary<string, object?>>>(StringComparer.OrdinalIgnoreCase);

                foreach (var mapping in _mappings)
                {
                    if (!_fieldMap.TryGetValue(mapping.Field, out var field))
                    {
                        continue;
                    }

                    if (!mapping.Field.Contains('>'))
                    {
                        var raw = GetTopLevelValue(source, mapping.Field);
                        var value = ConvertEditableValue(raw, field, mapping.Field, errors);
                        if (!ImportCellConverters.IsEmpty(value))
                        {
                            resultDict[mapping.Field] = value;
                        }

                        continue;
                    }

                    var parts = mapping.Field.Split('>', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 2)
                    {
                        continue;
                    }

                    var sourceRows = GetChildRows(source, parts[0]);
                    if (sourceRows.Count == 0)
                    {
                        continue;
                    }

                    if (!childRowsByParent.TryGetValue(parts[0], out var targetRows))
                    {
                        targetRows = [];
                        childRowsByParent[parts[0]] = targetRows;
                    }

                    for (var index = 0; index < sourceRows.Count; index++)
                    {
                        while (targetRows.Count <= index)
                        {
                            targetRows.Add(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase));
                        }

                        var raw = GetDictionaryValue(sourceRows[index], parts[1]);
                        var value = ConvertEditableValue(raw, field, mapping.Field, errors);
                        if (!ImportCellConverters.IsEmpty(value))
                        {
                            targetRows[index][parts[1]] = value;
                        }
                    }
                }

                foreach (var (parent, rows) in childRowsByParent)
                {
                    var notEmptyRows = rows
                        .Where(row => row.Values.Any(value => !ImportCellConverters.IsEmpty(value)))
                        .ToList();
                    if (notEmptyRows.Count > 0)
                    {
                        resultDict[parent] = notEmptyRows;
                    }
                }

                return result;
            }

            private object? ConvertCellValue(ICell? cell, FieldDef field, out string? error)
            {
                error = null;
                var text = ImportCellConverters.GetCellText(cell);
                if (string.IsNullOrWhiteSpace(text))
                {
                    return null;
                }

                try
                {
                    return field.Type switch
                    {
                        FieldType.Number => ImportCellConverters.ConvertNumber(cell, text),
                        FieldType.TimeStamp => ImportCellConverters.ConvertTimestamp(cell, text),
                        FieldType.Radio or FieldType.Select1 => ImportCellConverters.ConvertSingleOption(text, field),
                        FieldType.CheckBox or FieldType.Select2 => ImportCellConverters.ConvertMultiOption(text, field),
                        FieldType.ImageUpload or FieldType.FileUpload => ImportCellConverters.ConvertUrlList(text),
                        _ => ImportCellConverters.ConvertTextOrJson(text),
                    };
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    return null;
                }
            }

            private object? ConvertEditableValue(object? raw, FieldDef field, string fieldKey, List<FormDataImportCellError> errors)
            {
                raw = ImportCellConverters.UnwrapJsonValue(raw);
                if (ImportCellConverters.IsEmpty(raw))
                {
                    return null;
                }

                var value = raw!;
                try
                {
                    return field.Type switch
                    {
                        FieldType.Number => ImportCellConverters.ConvertEditableNumber(value),
                        FieldType.TimeStamp => ImportCellConverters.ConvertEditableTimestamp(value),
                        FieldType.Radio or FieldType.Select1 => ImportCellConverters.ConvertSingleOption(ImportCellConverters.ToCellText(value), field),
                        FieldType.CheckBox or FieldType.Select2 => ImportCellConverters.ConvertEditableMultiOption(value, field),
                        FieldType.ImageUpload or FieldType.FileUpload => ImportCellConverters.ConvertEditableUrlList(value),
                        _ => value is string text ? ImportCellConverters.ConvertTextOrJson(text) : value,
                    };
                }
                catch (Exception ex)
                {
                    errors.Add(new FormDataImportCellError
                    {
                        Field = fieldKey,
                        FieldTitle = ResolveFieldTitle(fieldKey),
                        Message = ex.Message,
                    });
                    return null;
                }
            }

            private static object? GetValue(ExpandoObject data, string? field)
            {
                if (string.IsNullOrWhiteSpace(field) || field.Contains('>'))
                {
                    return null;
                }

                return GetTopLevelValue(data, field);
            }

            private static object? GetTopLevelValue(ExpandoObject data, string field)
            {
                var dict = (IDictionary<string, object?>)data;
                return GetDictionaryValue(dict, field);
            }

            private static object? GetDictionaryValue(IDictionary<string, object?> data, string field)
            {
                return data.TryGetValue(field, out var value) ? ImportCellConverters.UnwrapJsonValue(value) : null;
            }

            private static List<IDictionary<string, object?>> GetChildRows(ExpandoObject data, string parentField)
            {
                var raw = GetTopLevelValue(data, parentField);
                raw = ImportCellConverters.UnwrapJsonValue(raw);
                if (raw is not IEnumerable rows || raw is string)
                {
                    return [];
                }

                return rows
                    .Cast<object?>()
                    .Select(AsDictionary)
                    .Where(x => x != null)
                    .Cast<IDictionary<string, object?>>()
                    .ToList();
            }

            private static IDictionary<string, object?>? AsDictionary(object? value)
            {
                value = ImportCellConverters.UnwrapJsonValue(value);
                if (value is ExpandoObject expando)
                {
                    return (IDictionary<string, object?>)expando;
                }
                if (value is IDictionary<string, object?> typed)
                {
                    return typed;
                }
                if (value is IDictionary dictionary)
                {
                    var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        if (entry.Key is string key)
                        {
                            result[key] = ImportCellConverters.UnwrapJsonValue(entry.Value);
                        }
                    }

                    return result;
                }

                return null;
            }

            private static ExpandoObject CloneData(ExpandoObject source)
            {
                var clone = new ExpandoObject();
                var cloneDict = (IDictionary<string, object?>)clone;
                foreach (var (key, value) in (IDictionary<string, object?>)source)
                {
                    cloneDict[key] = ImportCellConverters.UnwrapJsonValue(value);
                }

                return clone;
            }

            private static void MergeData(ExpandoObject target, ExpandoObject source)
            {
                var targetDict = (IDictionary<string, object?>)target;
                foreach (var (key, value) in (IDictionary<string, object?>)source)
                {
                    targetDict[key] = value;
                }
            }

            private static bool IsRequired(FieldDef field)
            {
                return field.Required || field.Props.Required == true;
            }

            private static Dictionary<string, FieldDef> BuildFieldMap(string? fieldSnapshotJson, FormDef? formDef)
            {
                var map = new Dictionary<string, FieldDef>(StringComparer.OrdinalIgnoreCase);
                List<FormDataImportFieldSnapshot> snapshot = string.IsNullOrWhiteSpace(fieldSnapshotJson)
                    ? []
                    : fieldSnapshotJson.DeserializeFromJson<List<FormDataImportFieldSnapshot>>() ?? [];
                if (snapshot.Count > 0)
                {
                    foreach (var item in snapshot)
                    {
                        map[item.Field] = new FieldDef
                        {
                            Field = item.Field,
                            Title = item.Title,
                            Type = item.Type,
                            Required = item.Required,
                            Props = new FieldProp { Options = item.Options },
                        };
                    }

                    return map;
                }

                foreach (var field in formDef?.Content?.Items ?? [])
                {
                    if (field.Type == FieldType.TableForm)
                    {
                        foreach (var sub in field.Columns ?? [])
                        {
                            map[$"{field.Field}>{sub.Field}"] = sub;
                        }
                    }
                    else
                    {
                        map[field.Field] = field;
                    }
                }

                return map;
            }

            private string ResolveFieldTitle(string? field)
            {
                if (string.IsNullOrWhiteSpace(field))
                {
                    return string.Empty;
                }

                if (_fieldMap.TryGetValue(field, out var fieldDef))
                {
                    return fieldDef.Title;
                }

                if (!field.Contains('>'))
                {
                    return field;
                }

                var parts = field.Split('>', 2, StringSplitOptions.RemoveEmptyEntries);
                var parent = _formDefService.Get(_importLog.FormId)?.Content?.Items?.FirstOrDefault(x => x.Field == parts[0]);
                var child = parent?.Columns?.FirstOrDefault(x => x.Field == parts[1]);
                return parent != null && child != null ? $"{parent.Title}.{child.Title}" : field;
            }

            internal static ErrorReport BuildErrorReport(List<ImportRecord> rows)
            {
                var workbook = new XSSFWorkbook();
                var sheet = workbook.CreateSheet("错误数据");
                var header = sheet.CreateRow(0);
                header.CreateCell(0).SetCellValue("行号");
                header.CreateCell(1).SetCellValue("错误详情");
                header.CreateCell(2).SetCellValue("数据");

                for (var i = 0; i < rows.Count; i++)
                {
                    var row = sheet.CreateRow(i + 1);
                    var item = rows[i];
                    row.CreateCell(0).SetCellValue(item.EndRowNumber.HasValue
                        ? $"{item.StartRowNumber}-{item.EndRowNumber}"
                        : item.StartRowNumber.ToString());
                    row.CreateCell(1).SetCellValue(string.Join(Environment.NewLine, item.Errors.Select(x => string.IsNullOrWhiteSpace(x.FieldTitle) ? x.Message : $"{x.FieldTitle}: {x.Message}")));
                    row.CreateCell(2).SetCellValue(item.Data.SerializeToJson());
                }

                using var ms = new MemoryStream();
                workbook.Write(ms, leaveOpen: true);
                return new ErrorReport
                {
                    FileName = $"导入错误报告_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx",
                    Content = ms.ToArray(),
                };
            }

            private static string BuildSuccessDetail(ImportRunResult result)
            {
                var changed = result.AddCount + result.UpdateCount;
                return $"数据导入完成，共新增 {result.AddCount} 条，更新 {result.UpdateCount} 条，成功 {changed} 条";
            }

            private static string BuildFailedDetail(ImportRunResult result)
            {
                return $"数据导入完成，新增 {result.AddCount} 条，更新 {result.UpdateCount} 条，失败 {result.ErrorRows.Count} 条";
            }
        }

        internal sealed class ImportRunResult
        {
            public long ProcessedCount { get; set; }

            public long AddCount { get; set; }

            public long UpdateCount { get; set; }

            public List<ImportRecord> ErrorRows { get; } = [];
        }

        internal sealed class ImportRecord
        {
            public int RecordIndex { get; set; }

            public int StartRowNumber { get; set; }

            public int? EndRowNumber { get; set; }

            public FormDataImportRowAction RowAction { get; set; }

            public string? MatchedDataId { get; set; }

            public string? MatchValue { get; set; }

            public ExpandoObject Data { get; set; } = new();

            public List<FormDataImportCellError> Errors { get; set; } = [];

            public ImportRecord WithError(string? field, string message)
            {
                Errors.Add(new FormDataImportCellError { Field = field, Message = message });
                return this;
            }

            public ImportRecord WithErrors(List<FormDataImportCellError> errors)
            {
                Errors = errors;
                return this;
            }

            public FormDataImportEditableErrorRow ToEditableRow()
            {
                return new FormDataImportEditableErrorRow
                {
                    RecordIndex = RecordIndex,
                    StartRowNumber = StartRowNumber,
                    EndRowNumber = EndRowNumber,
                    DataId = MatchedDataId,
                    Data = Data,
                    Errors = Errors,
                };
            }
        }

        private sealed class FormDataImportFieldSnapshot
        {
            public string Field { get; set; } = string.Empty;

            public string Title { get; set; } = string.Empty;

            public string Type { get; set; } = string.Empty;

            public List<ValueOption>? Options { get; set; }

            public bool Required { get; set; }
        }

        private sealed record ImportCellValue(object? Value, string? Error);

        internal sealed class ErrorReport
        {
            public string FileName { get; set; } = string.Empty;

            public byte[] Content { get; set; } = [];
        }
    }
}
