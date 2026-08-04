using System.Collections;
using System.Dynamic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Component;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Contracts;
using EIMSNext.Storage.Abstractions;
using HKH.Common;
using HKH.Mef2.Integration;
using MongoDB.Bson;
using MongoDB.Driver;
using NPOI.SS.UserModel;

namespace EIMSNext.ApiService
{
    public class FormDataApiService : ApiServiceBase<FormData, FormData, IFormDataService>
    {
        private const int ImportMaxColumns = 500;
        private const int ImportPreviewRowLimit = 30;
        private IFormDefService _formDefService;
        private IFormDataChangeLogService _formDataChangeLogService;
        private AdminPermissionEvaluator _permissionEvaluator;
        public FormDataApiService(IResolver resolver) : base(resolver)
        {
            _formDefService = resolver.Resolve<IFormDefService>();
            _formDataChangeLogService = resolver.Resolve<IFormDataChangeLogService>();
            _permissionEvaluator = resolver.Resolve<AdminPermissionEvaluator>();
        }

        public override Task AddAsync(FormData entity)
        {
            throw new UnLogException("Please use AddAsync(FormData,DataAction) instead");
        }
        public Task AddAsync(FormData entity, DataAction action)
        {
            ServiceContext.Action = action;
            return base.AddAsync(entity);
        }

        public Task ReplaceAsync(FormData entity, DataAction action)
        {
            ServiceContext.Action = action;
            return base.ReplaceAsync(entity);
        }

        public async Task<ExportResponse> ExportAsync(FormDataExportRequest request)
        {
            ValidateExportRequest(request);

            var totalCount = await CountExportAsync(request);
            var actualFormat = totalCount > 100000 ? ExportFormat.Csv : request.Format;
            var createBy = IdentityContext.CurrentEmployee?.Id ?? string.Empty;
            var columnsJson = request.Columns.SerializeToJson();
            var filterJson = request.SerializeToJson();
            var dedupKey = BuildDedupKey(new
            {
                ExportType = ExportType.FormData,
                request.Format,
                ActualFormat = actualFormat,
                request.FormId,
                request.AuthGroupId,
                request.Columns,
                request.Filter,
            });

            var exportLogService = Resolver.Resolve<IExportLogService>();
            var duplicated = await exportLogService.GetDuplicatedPendingAsync(IdentityContext.CurrentCorpId, createBy, dedupKey);
            if (duplicated != null)
            {
                return new ExportResponse
                {
                    TaskId = duplicated.Id,
                    IsDuplicate = true,
                    ActualFormat = duplicated.ActualFormat,
                    Message = "已有相同条件的导出任务正在处理中",
                };
            }

            var exportLog = new ExportLog
            {
                CorpId = IdentityContext.CurrentCorpId,
                ExportType = ExportType.FormData,
                RequestedFormat = request.Format,
                ActualFormat = actualFormat,
                Status = ExportLogStatus.Pending,
                ColumnsJson = columnsJson,
                FilterJson = filterJson,
                DedupKey = dedupKey,
                TotalCount = totalCount,
            };

            await exportLogService.AddAsync(exportLog);
            await Resolver.Resolve<IMessagePublisher>().PublishAsync(new DataExportTaskArgs
            {
                ExportLogId = exportLog.Id,
                CorpId = exportLog.CorpId ?? string.Empty,
            });

            return new ExportResponse
            {
                TaskId = exportLog.Id,
                IsDuplicate = false,
                ActualFormat = actualFormat,
                Message = actualFormat != request.Format ? "超过 10W 行，已自动切换为 CSV 导出" : null,
            };
        }

        public FormDataImportPreviewResponse PreviewImport(string formId, Stream source, string fileName, long fileSize)
        {
            ValidateImportFile(fileName, fileSize);
            if (string.IsNullOrWhiteSpace(formId))
            {
                throw new ArgumentException("表单ID不能为空");
            }

            BuildImportPermissionContext(formId, authGroupId: null);
            _ = _formDefService.Get(formId) ?? throw new ArgumentException("表单不存在或已被删除");

            using var workbook = WorkbookFactory.Create(source);
            var formatter = new DataFormatter();
            var response = new FormDataImportPreviewResponse();
            for (var sheetIndex = 0; sheetIndex < workbook.NumberOfSheets; sheetIndex++)
            {
                var sheet = workbook.GetSheetAt(sheetIndex);
                var rowCount = sheet.LastRowNum >= sheet.FirstRowNum ? sheet.LastRowNum + 1 : 0;
                var previewRowCount = Math.Min(rowCount, ImportPreviewRowLimit);
                var columnCount = 0;
                var rows = new List<IRow?>();
                for (var rowIndex = 0; rowIndex < previewRowCount; rowIndex++)
                {
                    var row = sheet.GetRow(rowIndex);
                    rows.Add(row);
                    columnCount = Math.Max(columnCount, row?.LastCellNum ?? 0);
                }

                if (columnCount > ImportMaxColumns)
                {
                    throw new ArgumentException("导入列数不能超过 500 列");
                }

                response.Sheets.Add(new FormDataImportSheetPreview
                {
                    Name = sheet.SheetName,
                    RowCount = rowCount,
                    ColumnCount = columnCount,
                    Rows = rows.Select(row => ReadPreviewRow(row, columnCount, formatter)).ToList(),
                });
            }

            return response;
        }

        public async Task<FormDataImportStartResponse> StartImportAsync(FormDataImportStartRequest request, Stream source, string fileName, long fileSize)
        {
            ValidateImportRequest(request, fileName, fileSize);
            var permission = BuildImportPermissionContext(request.FormId, request.AuthGroupId);

            var formDef = _formDefService.Get(request.FormId) ?? throw new ArgumentException("表单不存在或已被删除");
            var fieldSnapshot = BuildImportFieldSnapshot(formDef, permission.FieldPerms);
            ValidateImportMappings(request, fieldSnapshot);
            var importLogService = Resolver.Resolve<IFormDataImportLogService>();
            var storage = Resolver.Resolve<IStorageProvider>();
            var importLogId = ObjectId.GenerateNewId().ToString();
            var normalizedFileName = NormalizeFileName(fileName);
            var objectKey = $"Import\\{IdentityContext.CurrentCorpId}\\{DateTime.UtcNow:yyyyMMdd}\\{importLogId}_{normalizedFileName}";
            if (!storage.Upload(source, objectKey))
            {
                throw new InvalidOperationException("上传导入文件失败");
            }

            var action = formDef.UsingWorkflow
                ? request.TriggerWorkflow ? DataAction.Submit : DataAction.Save
                : DataAction.Submit;

            var importLog = new FormDataImportLog
            {
                Id = importLogId,
                CorpId = IdentityContext.CurrentCorpId,
                AppId = formDef.AppId,
                FormId = formDef.Id,
                FormName = formDef.Name,
                AuthGroupId = request.AuthGroupId,
                FormUsingWorkflow = formDef.UsingWorkflow,
                Mode = request.Mode,
                TriggerValidation = request.TriggerValidation,
                TriggerWorkflow = formDef.UsingWorkflow && request.TriggerWorkflow,
                ImportAction = action,
                Status = FormDataImportStatus.Pending,
                MatchField = request.MatchField,
                SheetName = request.SheetName,
                HeaderRowIndex = request.HeaderRowIndex,
                SourceFileName = normalizedFileName,
                SourceObjectKey = objectKey,
                SourceFileSize = fileSize,
                FieldSnapshotJson = fieldSnapshot.SerializeToJson(),
                MappingJson = request.Mappings.SerializeToJson(),
                DataScopeFilterJson = permission.DataScopeFilter?.SerializeToJson(),
            };

            await importLogService.AddAsync(importLog);
            await Resolver.Resolve<IMessagePublisher>().PublishAsync(new DataImportTaskArgs
            {
                ImportLogId = importLog.Id,
                CorpId = importLog.CorpId ?? string.Empty,
                RetryCount = importLog.RetryCount,
            });

            return new FormDataImportStartResponse
            {
                TaskId = importLog.Id,
                Message = "导入任务已创建",
            };
        }

        public FormDataImportStatusResponse GetImportStatus(string id)
        {
            var importLog = GetAccessibleImportLog(id);
            return new FormDataImportStatusResponse
            {
                TaskId = importLog.Id,
                Status = importLog.Status,
                TotalCount = importLog.TotalCount,
                ProcessedCount = importLog.ProcessedCount,
                AddCount = importLog.AddCount,
                UpdateCount = importLog.UpdateCount,
                FailedCount = importLog.FailedCount,
                ErrorMessage = importLog.ErrorMessage,
                ErrorReportDownloadUrl = importLog.ErrorReportDownloadUrl,
                CanEditErrors =
                    importLog.Status == FormDataImportStatus.CompletedWithErrors &&
                    importLog.EditableErrorRowCount > 0 &&
                    importLog.EditableErrorRowCount <= EIMSNext.Common.Constants.FormDataImportMaxEditableErrors,
                EditableErrorRowCount = importLog.EditableErrorRowCount,
            };
        }

        public FormDataImportEditableErrorsResponse GetEditableImportErrors(string id)
        {
            var importLog = GetAccessibleImportLog(id);
            EnsureImportErrorsEditable(importLog);
            return new FormDataImportEditableErrorsResponse
            {
                Rows = ReadEditableErrorRows(importLog),
            };
        }

        public async Task<FormDataImportRetryResponse> RetryImportAsync(string id, FormDataImportRetryRequest request)
        {
            var importLog = GetAccessibleImportLog(id);
            EnsureImportErrorsEditable(importLog);
            var permission = BuildImportPermissionContext(importLog.FormId, importLog.AuthGroupId);
            var rows = request.Rows ?? [];
            if (rows.Count == 0 || rows.Count > EIMSNext.Common.Constants.FormDataImportMaxEditableErrors)
            {
                throw new ArgumentException($"修正数据不能为空且不能超过 {EIMSNext.Common.Constants.FormDataImportMaxEditableErrors} 条");
            }

            var formDef = _formDefService.Get(importLog.FormId) ?? throw new ArgumentException("表单不存在或已被删除");
            var fieldMap = BuildImportFieldMap(importLog.FieldSnapshotJson, formDef);
            var dataScopeFilter = string.IsNullOrWhiteSpace(importLog.DataScopeFilterJson)
                ? permission.DataScopeFilter
                : importLog.DataScopeFilterJson.DeserializeFromJson<DynamicFilter>();
            var result = new FormDataImportRetryResponse { TaskId = importLog.Id };

            foreach (var (row, index) in rows.Select((row, index) => (row, index)))
            {
                var correctionDataId = importLog.Mode == FormDataImportMode.AddOnly ? null : row.DataId;
                var editableRow = new FormDataImportEditableErrorRow
                {
                    RecordIndex = index,
                    StartRowNumber = index + 1,
                    DataId = correctionDataId,
                };

                try
                {
                    var data = NormalizeImportCorrectionData(row.Data, importLog.MappingJson, fieldMap, out var conversionErrors);
                    editableRow.Data = data;
                    if (conversionErrors.Count > 0)
                    {
                        editableRow.Errors = conversionErrors;
                        result.Rows.Add(editableRow);
                        result.FailedCount++;
                        continue;
                    }

                    var shapeErrors = ValidateCorrectionRecordShape(importLog, data, fieldMap);
                    if (shapeErrors.Count > 0)
                    {
                        editableRow.Errors = shapeErrors;
                        result.Rows.Add(editableRow);
                        result.FailedCount++;
                        continue;
                    }

                    var matched = ResolveCorrectionTarget(importLog, correctionDataId, data, dataScopeFilter);
                    if (matched == null)
                    {
                        var validationErrors = ValidateCorrectionData(importLog, data, fieldMap);
                        if (validationErrors.Count > 0)
                        {
                            editableRow.Errors = validationErrors;
                            result.Rows.Add(editableRow);
                            result.FailedCount++;
                            continue;
                        }

                        await AddAsync(new FormData
                        {
                            CorpId = importLog.CorpId,
                            AppId = importLog.AppId,
                            FormId = importLog.FormId,
                            FlowStatus = FlowStatus.Draft,
                            Data = data,
                        }, importLog.ImportAction);
                        result.AddCount++;
                        continue;
                    }

                    var mergedData = CloneImportData(matched.Data);
                    MergeImportData(mergedData, data);
                    var mergedValidationErrors = ValidateCorrectionData(importLog, mergedData, fieldMap);
                    if (mergedValidationErrors.Count > 0)
                    {
                        editableRow.DataId = matched.Id;
                        editableRow.Data = data;
                        editableRow.Errors = mergedValidationErrors;
                        result.Rows.Add(editableRow);
                        result.FailedCount++;
                        continue;
                    }

                    MergeImportData(matched.Data, data);
                    await ReplaceAsync(matched, importLog.ImportAction);
                    result.UpdateCount++;
                }
                catch (Exception ex)
                {
                    editableRow.Errors = [new FormDataImportCellError { Message = ex.Message }];
                    result.Rows.Add(editableRow);
                    result.FailedCount++;
                }
            }

            var cumulativeAddCount = importLog.AddCount + result.AddCount;
            var cumulativeUpdateCount = importLog.UpdateCount + result.UpdateCount;
            var remainingFailedCount = Math.Max(0, importLog.FailedCount - (result.AddCount + result.UpdateCount));
            await Resolver.Resolve<IFormDataImportLogService>().MarkCorrectionResultAsync(
                importLog.Id,
                importLog.TotalCount,
                cumulativeAddCount,
                cumulativeUpdateCount,
                remainingFailedCount,
                result.Rows.Count > 0 ? result.Rows.SerializeToJson() : null,
                null,
                result.Rows.Count);

            return result;
        }

        private void EnsureImportErrorsEditable(FormDataImportLog importLog)
        {
            if (importLog.Status != FormDataImportStatus.CompletedWithErrors ||
                importLog.EditableErrorRowCount <= 0 ||
                importLog.EditableErrorRowCount > EIMSNext.Common.Constants.FormDataImportMaxEditableErrors)
            {
                throw new InvalidOperationException("当前导入任务没有可在线修改的失败数据");
            }
        }

        public async Task<FormDataFilterOptionsResponse> GetFilterOptionsAsync(FormDataFilterOptionsRequest request)
        {
            var filter = BuildBaseFilter(request);
            if (IdentityContext.IdentityType == IdentityType.Public)
            {
                var validator = Resolver.Resolve<IPublicAccessValidator>();
                if (!validator.CanQueryFormData(request.FormId) && !validator.CanReadDashboardForm(request.FormId))
                {
                    return new FormDataFilterOptionsResponse { Items = [] };
                }

                filter = validator.ApplyFormDataScope(request.FormId, request.Filter);
            }
            else
            {
                filter = ApplyFilterOptionsPermission(request, filter);
            }

            var field = DynamicField.FormatFieldForFilter($"data.{request.Field}", request.FieldType);
            var limit = request.Limit <= 0 ? 50 : Math.Min(request.Limit, 200);

            var query = new FilterOptionQuery
            {
                Filter = filter,
                FieldPath = field,
                Keyword = request.Keyword,
                Limit = limit
            };

            var result = await CoreService.GetFieldOptionsAsync(query);
            return new FormDataFilterOptionsResponse { Items = result.Items };
        }

        public List<FormDataChangeLog> GetChangeLogs(string dataId, int skip, int top)
        {
            if (string.IsNullOrWhiteSpace(dataId)) return [];

            skip = Math.Max(skip, 0);
            top = Math.Clamp(top <= 0 ? 20 : top, 1, 200);

            return _formDataChangeLogService
                .Query(x => x.CorpId == IdentityContext.CurrentCorpId && x.DataId == dataId && !x.DeleteFlag)
                .OrderByDescending(x => x.OperateTime)
                .Skip(skip)
                .Take(top)
                .ToList();
        }

        public long CountChangeLogs(string dataId)
        {
            if (string.IsNullOrWhiteSpace(dataId)) return 0;

            return _formDataChangeLogService.Count(x => x.CorpId == IdentityContext.CurrentCorpId && x.DataId == dataId && !x.DeleteFlag);
        }

        private DynamicFilter BuildBaseFilter(FormDataFilterOptionsRequest request)
        {
            var baseFilter = new DynamicFilter
            {
                Rel = FilterRel.And,
                Items =
                [
                    new DynamicFilter { Field = Fields.FormId, Op = FilterOp.Eq, Value = request.FormId },
                    new DynamicFilter { Field = Fields.CorpId, Op = FilterOp.Eq, Value = IdentityContext.CurrentCorpId },
                    new DynamicFilter { Field = Fields.DeleteFlag, Op = FilterOp.Ne, Value = true }
                ]
            };

            var userFilter = request.Filter;
            if (userFilter != null && (userFilter.IsGroup || !string.IsNullOrEmpty(userFilter.Field)))
            {
                baseFilter.Items.Add(userFilter);
            }

            return baseFilter;
        }

        private DynamicFilter ApplyFilterOptionsPermission(FormDataFilterOptionsRequest request, DynamicFilter filter)
        {
            if (_permissionEvaluator.HasUnrestrictedManagementIdentity)
            {
                return filter;
            }

            var authGroups = _permissionEvaluator.GetUsageAuthGroupsForCurrentEmployee(request.FormId)
                .Where(HasInheritedDataAccess)
                .Where(group => string.IsNullOrWhiteSpace(request.AuthGroupId) ||
                    string.Equals(group.Id, request.AuthGroupId, StringComparison.OrdinalIgnoreCase))
                .Where(group => GetEffectiveDataPerms(group).HasFlag(DataPerms.View))
                .ToList();
            if (authGroups.Count == 0)
            {
                return filter.And(CreateNoMatchFilter())!;
            }

            return filter.And(BuildDataScopeFilter(authGroups))!;
        }

        private Task<long> CountExportAsync(FormDataExportRequest request)
        {
            return CountAsync(request.Filter ?? DynamicFilter.Empty);
        }

        private void ValidateImportRequest(FormDataImportStartRequest request, string fileName, long fileSize)
        {
            ValidateImportFile(fileName, fileSize);
            if (string.IsNullOrWhiteSpace(request.FormId))
            {
                throw new ArgumentException("表单ID不能为空");
            }

            if (request.HeaderRowIndex <= 0)
            {
                throw new ArgumentException("标题行必须大于 0");
            }

            request.Mappings = request.Mappings
                .Where(x => x.ColumnIndex >= 0 && !string.IsNullOrWhiteSpace(x.Field))
                .GroupBy(x => x.ColumnIndex)
                .Select(x => x.First())
                .ToList();

            if (request.Mappings.Count == 0)
            {
                throw new ArgumentException("字段映射不能为空");
            }

            var duplicateField = request.Mappings
                .GroupBy(x => x.Field, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(x => x.Count() > 1);
            if (duplicateField != null)
            {
                throw new ArgumentException($"字段不能重复映射：{duplicateField.Key}");
            }

            if (request.Mode != FormDataImportMode.AddOnly && string.IsNullOrWhiteSpace(request.MatchField))
            {
                throw new ArgumentException("更新导入需要选择匹配字段");
            }

            if (!string.IsNullOrWhiteSpace(request.MatchField) &&
                request.Mappings.All(x => !string.Equals(x.Field, request.MatchField, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException("匹配字段必须包含在字段映射中");
            }
        }

        private static void ValidateImportMappings(FormDataImportStartRequest request, IReadOnlyCollection<FormDataImportFieldSnapshot> fieldSnapshot)
        {
            var fieldMap = fieldSnapshot.ToDictionary(x => x.Field, StringComparer.OrdinalIgnoreCase);
            request.Mappings = request.Mappings
                .Where(x => fieldMap.ContainsKey(x.Field))
                .ToList();
            if (request.Mappings.Count == 0)
            {
                throw new ArgumentException("没有可导入的字段");
            }

            if (!string.IsNullOrWhiteSpace(request.MatchField) &&
                (!fieldMap.ContainsKey(request.MatchField) ||
                 request.Mappings.All(x => !string.Equals(x.Field, request.MatchField, StringComparison.OrdinalIgnoreCase))))
            {
                throw new ArgumentException("匹配字段不存在、不可导入或无权限");
            }
        }

        internal static void ValidateImportFile(string fileName, long fileSize)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            var maxSize = ext switch
            {
                ".xlsx" => 20L * 1024 * 1024,
                ".xls" => 5L * 1024 * 1024,
                _ => throw new ArgumentException("仅支持 xlsx 或 xls 文件"),
            };
            if (fileSize <= 0 || fileSize > maxSize)
            {
                throw new ArgumentException(ext == ".xlsx" ? "xlsx 文件不能超过 20MB" : "xls 文件不能超过 5MB");
            }
        }

        private static List<string> ReadPreviewRow(IRow? row, int columnCount, DataFormatter formatter)
        {
            var result = new List<string>(columnCount);
            for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                result.Add(row == null ? string.Empty : formatter.FormatCellValue(row.GetCell(columnIndex))?.Trim() ?? string.Empty);
            }

            return result;
        }

        private void ValidateExportRequest(FormDataExportRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FormId))
            {
                throw new ArgumentException("表单ID不能为空");
            }

            if (request.Columns == null || request.Columns.Count == 0)
            {
                throw new ArgumentException("导出列不能为空");
            }

            request.Columns = request.Columns
                .Where(x => !string.IsNullOrWhiteSpace(x.Key) && !string.IsNullOrWhiteSpace(x.Header))
                .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .ToList();

            if (request.Columns.Count == 0)
            {
                throw new ArgumentException("导出列不能为空");
            }

            var formDef = _formDefService.Get(request.FormId) ?? throw new ArgumentException("表单不存在或已被删除");
            var fields = formDef.Content?.Items ?? [];
            request.Columns = request.Columns
                .Where(column => !IsDataSelectFieldPath(column.Key, fields))
                .ToList();

            if (request.Columns.Count == 0)
            {
                throw new BadRequestException("数据选择字段不能导出");
            }

            foreach (var column in request.Columns)
            {
                column.Type = ResolveColumnType(column.Key, fields);
            }
        }

        private FormImportPermissionContext BuildImportPermissionContext(string formId, string? authGroupId)
        {
            if (_permissionEvaluator.HasUnrestrictedManagementIdentity)
            {
                return new FormImportPermissionContext(null, null);
            }

            var groups = _permissionEvaluator.GetUsageAuthGroupsForCurrentEmployee(formId)
                .Where(HasInheritedDataAccess)
                .Where(group => string.IsNullOrWhiteSpace(authGroupId) ||
                    string.Equals(group.Id, authGroupId, StringComparison.OrdinalIgnoreCase))
                .Where(group => GetEffectiveDataPerms(group).HasFlag(DataPerms.Import))
                .ToList();
            if (groups.Count == 0)
            {
                throw new ArgumentException("当前用户无该表单的导入权限");
            }

            return new FormImportPermissionContext(
                MergeFieldPerms(groups),
                BuildDataScopeFilter(groups));
        }

        private DynamicFilter? BuildDataScopeFilter(IEnumerable<AuthGroup> authGroups)
        {
            var rangeFilters = new List<DynamicFilter>();
            foreach (var authGroup in authGroups)
            {
                var groupFilter = BuildAuthGroupDataFilter(authGroup);
                if (groupFilter == null || groupFilter.IsEmpty)
                {
                    return null;
                }

                rangeFilters.Add(groupFilter);
            }

            return OrFilters(rangeFilters) ?? CreateNoMatchFilter();
        }

        private DynamicFilter? BuildAuthGroupDataFilter(AuthGroup authGroup)
        {
            switch (authGroup.Type)
            {
                case AuthGroupType.ManageSelfData:
                    if (string.IsNullOrWhiteSpace(IdentityContext.CurrentEmployee?.Id))
                    {
                        return CreateNoMatchFilter();
                    }

                    return new DynamicFilter
                    {
                        Field = Fields.CreateById,
                        Op = FilterOp.Eq,
                        Value = IdentityContext.CurrentEmployee.Id,
                    };
                case AuthGroupType.ViewAllData:
                case AuthGroupType.ManageAllData:
                    return null;
                case AuthGroupType.Custom:
                    if (string.IsNullOrWhiteSpace(authGroup.DataFilter))
                    {
                        return null;
                    }

                    var condList = authGroup.DataFilter.DeserializeFromJson<ConditionList>();
                    return condList?.ToDynamicFilter();
                default:
                    return null;
            }
        }

        private static DynamicFilter CreateNoMatchFilter()
        {
            return new DynamicFilter
            {
                Field = Fields.BsonId,
                Op = FilterOp.Eq,
                Value = "__no_permission__",
            };
        }

        private static DynamicFilter? OrFilters(IEnumerable<DynamicFilter?> filters)
        {
            var list = filters
                .Where(x => x != null && !x.IsEmpty)
                .Cast<DynamicFilter>()
                .ToList();
            if (list.Count == 0)
            {
                return null;
            }

            if (list.Count == 1)
            {
                return list[0];
            }

            return new DynamicFilter
            {
                Rel = FilterRel.Or,
                Items = list,
            };
        }

        private static List<FieldPerm>? MergeFieldPerms(IEnumerable<AuthGroup> authGroups)
        {
            var groups = authGroups.ToList();
            if (groups.Any(x => x.FieldPerms == null || x.FieldPerms.Count == 0))
            {
                return null;
            }

            var merged = new Dictionary<string, FieldPerm>(StringComparer.OrdinalIgnoreCase);
            foreach (var fieldPerm in groups.SelectMany(x => x.FieldPerms))
            {
                if (!merged.TryGetValue(fieldPerm.Id, out var current))
                {
                    merged[fieldPerm.Id] = new FieldPerm
                    {
                        Id = fieldPerm.Id,
                        Visible = fieldPerm.Visible,
                        Editable = fieldPerm.Editable,
                        TableInsert = fieldPerm.TableInsert,
                        TableEdit = fieldPerm.TableEdit,
                        TableDelete = fieldPerm.TableDelete,
                    };
                    continue;
                }

                current.Visible |= fieldPerm.Visible;
                current.Editable |= fieldPerm.Editable;
                current.TableInsert = MergeNullablePermission(current.TableInsert, fieldPerm.TableInsert);
                current.TableEdit = MergeNullablePermission(current.TableEdit, fieldPerm.TableEdit);
                current.TableDelete = MergeNullablePermission(current.TableDelete, fieldPerm.TableDelete);
            }

            return merged.Values.ToList();
        }

        private static bool? MergeNullablePermission(bool? current, bool? next)
        {
            if (current == true || next == true)
            {
                return true;
            }

            if (current.HasValue || next.HasValue)
            {
                return false;
            }

            return null;
        }

        // keep in sync with FormDataController.cs:1258-1267
        private static DataPerms GetEffectiveDataPerms(AuthGroup authGroup)
        {
            return authGroup.Type switch
            {
                AuthGroupType.ManageSelfData => DataPerms.All,
                AuthGroupType.ManageAllData => DataPerms.All,
                AuthGroupType.ViewAllData => DataPerms.View,
                _ => (DataPerms)authGroup.DataPerms,
            };
        }

        // keep in sync with FormDataController.cs:1269-1272
        private static bool HasInheritedDataAccess(AuthGroup authGroup)
        {
            return GetEffectiveDataPerms(authGroup) != DataPerms.None;
        }

        private FormDataImportLog GetAccessibleImportLog(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("导入任务ID不能为空");
            }

            var importLog = Resolver.Resolve<IFormDataImportLogService>().Get(id);
            if (importLog == null ||
                !string.Equals(importLog.CorpId, IdentityContext.CurrentCorpId, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("导入任务不存在或无权访问");
            }
            if (_permissionEvaluator.HasUnrestrictedManagementIdentity)
            {
                return importLog;
            }

            var currentEmployeeId = IdentityContext.CurrentEmployee?.Id;
            var ownerId = !string.IsNullOrWhiteSpace(importLog.CreateBy?.Id)
                ? importLog.CreateBy.Id
                : importLog.CreateBy?.Value;
            if (!string.IsNullOrWhiteSpace(currentEmployeeId) &&
                !string.IsNullOrWhiteSpace(ownerId) &&
                string.Equals(currentEmployeeId, ownerId, StringComparison.OrdinalIgnoreCase))
            {
                return importLog;
            }

            throw new ArgumentException("导入任务不存在或无权访问");
        }

        private List<FormDataImportEditableErrorRow> ReadEditableErrorRows(FormDataImportLog importLog)
        {
            var json = importLog.EditableErrorRowsJson;
            if (string.IsNullOrWhiteSpace(json) && !string.IsNullOrWhiteSpace(importLog.EditableErrorRowsObjectKey))
            {
                using var stream = Resolver.Resolve<IStorageProvider>().Download(importLog.EditableErrorRowsObjectKey);
                if (stream != null)
                {
                    using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: false);
                    json = reader.ReadToEnd();
                }
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return [];
            }

            return json.DeserializeFromJson<List<FormDataImportEditableErrorRow>>() ?? [];
        }

        private static Dictionary<string, FieldDef> BuildImportFieldMap(string? fieldSnapshotJson, FormDef formDef)
        {
            var map = new Dictionary<string, FieldDef>(StringComparer.OrdinalIgnoreCase);
            var snapshot = string.IsNullOrWhiteSpace(fieldSnapshotJson)
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

            foreach (var field in formDef.Content?.Items ?? [])
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

        private static ExpandoObject NormalizeImportCorrectionData(
            ExpandoObject source,
            string? mappingJson,
            IReadOnlyDictionary<string, FieldDef> fieldMap,
            out List<FormDataImportCellError> errors)
        {
            errors = [];
            var result = new ExpandoObject();
            var resultDict = (IDictionary<string, object?>)result;
            var sourceDict = (IDictionary<string, object?>)source;
            var mappings = string.IsNullOrWhiteSpace(mappingJson)
                ? []
                : mappingJson.DeserializeFromJson<List<FormDataImportMappingItem>>() ?? [];
            var childRowsByParent = new Dictionary<string, List<Dictionary<string, object?>>>(StringComparer.OrdinalIgnoreCase);

            foreach (var mapping in mappings)
            {
                if (!fieldMap.TryGetValue(mapping.Field, out var field))
                {
                    continue;
                }

                if (!mapping.Field.Contains('>'))
                {
                    var raw = GetDictionaryValue(sourceDict, mapping.Field);
                    var value = ConvertImportCorrectionValue(raw, field, mapping.Field, errors);
                    if (!IsImportValueEmpty(value))
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

                var sourceRows = GetImportChildRows(source, parts[0]);
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
                    var value = ConvertImportCorrectionValue(raw, field, mapping.Field, errors);
                    if (!IsImportValueEmpty(value))
                    {
                        targetRows[index][parts[1]] = value;
                    }
                }
            }

            foreach (var (parent, rows) in childRowsByParent)
            {
                var notEmptyRows = rows
                    .Where(row => row.Values.Any(value => !IsImportValueEmpty(value)))
                    .ToList();
                if (notEmptyRows.Count > 0)
                {
                    resultDict[parent] = notEmptyRows;
                }
            }

            return result;
        }

        private static object? ConvertImportCorrectionValue(object? raw, FieldDef field, string fieldKey, List<FormDataImportCellError> errors)
        {
            raw = UnwrapImportJsonValue(raw);
            if (IsImportValueEmpty(raw))
            {
                return null;
            }

            try
            {
                return field.Type switch
                {
                    FieldType.Number => ConvertImportNumber(raw!),
                    FieldType.TimeStamp => ConvertImportTimestamp(raw!),
                    FieldType.Radio or FieldType.Select1 => ConvertImportSingleOption(ToImportCellText(raw), field),
                    FieldType.CheckBox or FieldType.Select2 => ConvertImportMultiOption(raw!, field),
                    FieldType.ImageUpload or FieldType.FileUpload => ConvertImportUrlList(raw!),
                    _ => raw is string text ? ConvertImportTextOrJson(text) : raw,
                };
            }
            catch (Exception ex)
            {
                errors.Add(new FormDataImportCellError
                {
                    Field = fieldKey,
                    FieldTitle = ResolveImportFieldTitle(fieldKey, fieldMap: null),
                    Message = ex.Message,
                });
                return null;
            }
        }

        private static List<FormDataImportCellError> ValidateCorrectionRecordShape(
            FormDataImportLog importLog,
            ExpandoObject data,
            IReadOnlyDictionary<string, FieldDef> fieldMap)
        {
            var errors = new List<FormDataImportCellError>();
            if (importLog.Mode == FormDataImportMode.AddOnly)
            {
                return errors;
            }

            var value = GetImportValue(data, importLog.MatchField);
            if (IsImportValueEmpty(value))
            {
                errors.Add(new FormDataImportCellError
                {
                    Field = importLog.MatchField,
                    FieldTitle = ResolveImportFieldTitle(importLog.MatchField, fieldMap),
                    Message = "匹配字段不能为空",
                });
            }

            return errors;
        }

        private static List<FormDataImportCellError> ValidateCorrectionData(
            FormDataImportLog importLog,
            ExpandoObject data,
            IReadOnlyDictionary<string, FieldDef> fieldMap)
        {
            if (!importLog.TriggerValidation)
            {
                return [];
            }

            var errors = new List<FormDataImportCellError>();
            foreach (var (fieldKey, field) in fieldMap)
            {
                if (!IsImportFieldRequired(field))
                {
                    continue;
                }

                if (!fieldKey.Contains('>'))
                {
                    if (IsImportValueEmpty(GetImportTopLevelValue(data, fieldKey)))
                    {
                        errors.Add(new FormDataImportCellError
                        {
                            Field = fieldKey,
                            FieldTitle = ResolveImportFieldTitle(fieldKey, fieldMap),
                            Message = "必填字段不能为空",
                        });
                    }

                    continue;
                }

                var parts = fieldKey.Split('>', 2, StringSplitOptions.RemoveEmptyEntries);
                var rows = GetImportChildRows(data, parts[0]);
                if (rows.Count == 0)
                {
                    errors.Add(new FormDataImportCellError
                    {
                        Field = fieldKey,
                        FieldTitle = ResolveImportFieldTitle(fieldKey, fieldMap),
                        Message = "必填字段不能为空",
                    });
                    continue;
                }

                for (var index = 0; index < rows.Count; index++)
                {
                    if (IsImportValueEmpty(GetDictionaryValue(rows[index], parts[1])))
                    {
                        errors.Add(new FormDataImportCellError
                        {
                            Field = fieldKey,
                            FieldTitle = ResolveImportFieldTitle(fieldKey, fieldMap),
                            Message = $"第 {index + 1} 条明细不能为空",
                        });
                    }
                }
            }

            return errors;
        }

        private FormData? ResolveCorrectionTarget(FormDataImportLog importLog, string? dataId, ExpandoObject data, DynamicFilter? dataScopeFilter)
        {
            if (!string.IsNullOrWhiteSpace(dataId))
            {
                var existing = CoreService.Get(dataId);
                if (existing == null ||
                    existing.DeleteFlag ||
                    !string.Equals(existing.CorpId, importLog.CorpId, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(existing.FormId, importLog.FormId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("修正数据关联的原始数据不存在或无权访问");
                }
                if (dataScopeFilter != null && !dataScopeFilter.IsEmpty &&
                    !FindCorrectionDataById(importLog, dataId, dataScopeFilter).Any())
                {
                    throw new InvalidOperationException("修正数据关联的原始数据不存在或无权访问");
                }

                return existing;
            }

            if (importLog.Mode == FormDataImportMode.AddOnly)
            {
                return null;
            }

            var matchValue = GetImportValue(data, importLog.MatchField);
            if (IsImportValueEmpty(matchValue))
            {
                return null;
            }

            var found = FindCorrectionMatchedData(importLog, importLog.MatchField!, matchValue!, dataScopeFilter).ToList();
            if (found.Count > 1)
            {
                throw new InvalidOperationException($"匹配字段存在多条数据：{ToImportCellText(matchValue)}");
            }
            if (found.Count == 0 && importLog.Mode == FormDataImportMode.UpdateOnly)
            {
                throw new InvalidOperationException($"未找到匹配数据：{ToImportCellText(matchValue)}");
            }

            return found.FirstOrDefault();
        }

        private IEnumerable<FormData> FindCorrectionMatchedData(FormDataImportLog importLog, string field, object value, DynamicFilter? dataScopeFilter)
        {
            var filter = new DynamicFilter
            {
                Rel = FilterRel.And,
                Items =
                [
                    new DynamicFilter { Field = Fields.CorpId, Op = FilterOp.Eq, Value = importLog.CorpId },
                    new DynamicFilter { Field = Fields.FormId, Op = FilterOp.Eq, Value = importLog.FormId },
                    new DynamicFilter { Field = Fields.DeleteFlag, Op = FilterOp.Ne, Value = true },
                    new DynamicFilter { Field = $"{Fields.Data}.{field}", Op = FilterOp.Eq, Value = value },
                ]
            };
            if (dataScopeFilter != null && !dataScopeFilter.IsEmpty)
            {
                filter.Items!.Add(dataScopeFilter);
            }

            var found = CoreService.Find(new DynamicFindOptions<FormData>
            {
                Filter = filter,
                Take = 2,
            });
            return found.ToList();
        }

        private IEnumerable<FormData> FindCorrectionDataById(FormDataImportLog importLog, string dataId, DynamicFilter dataScopeFilter)
        {
            var filter = new DynamicFilter
            {
                Rel = FilterRel.And,
                Items =
                [
                    new DynamicFilter { Field = Fields.Id, Op = FilterOp.Eq, Value = dataId },
                    new DynamicFilter { Field = Fields.CorpId, Op = FilterOp.Eq, Value = importLog.CorpId },
                    new DynamicFilter { Field = Fields.FormId, Op = FilterOp.Eq, Value = importLog.FormId },
                    new DynamicFilter { Field = Fields.DeleteFlag, Op = FilterOp.Ne, Value = true },
                    dataScopeFilter,
                ]
            };

            var found = CoreService.Find(new DynamicFindOptions<FormData>
            {
                Filter = filter,
                Take = 1,
            });
            return ((IEnumerable<FormData>)found).ToList();
        }

        private static object ConvertImportNumber(object raw)
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
                _ => double.TryParse(ToImportCellText(raw), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var invariantValue)
                    ? invariantValue
                    : double.TryParse(ToImportCellText(raw), out var localValue)
                        ? localValue
                        : throw new FormatException("数字格式无效"),
            };
        }

        private static object ConvertImportTimestamp(object raw)
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
                return doubleValue > 10000000000
                    ? (long)doubleValue
                    : new DateTimeOffset(DateTime.FromOADate(doubleValue)).ToUnixTimeMilliseconds();
            }

            var text = ToImportCellText(raw);
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

        private static object ConvertImportSingleOption(string text, FieldDef field)
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

        private static object ConvertImportMultiOption(object raw, FieldDef field)
        {
            if (raw is IEnumerable enumerable && raw is not string)
            {
                var parts = enumerable
                    .Cast<object?>()
                    .Select(ToImportCellText)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();
                return ConvertImportMultiOption(string.Join(",", parts), field);
            }

            var items = ToImportCellText(raw)
                .Split([',', '，', ';', '；', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
            if (field.Props.Options == null || field.Props.Options.Count == 0)
            {
                return items;
            }

            return items.Select(item =>
            {
                var option = field.Props.Options.FirstOrDefault(x =>
                    string.Equals(x.Value, item, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x.Label, item, StringComparison.OrdinalIgnoreCase));
                return option?.Value ?? throw new FormatException($"选项不存在：{item}");
            }).ToList();
        }

        private static object ConvertImportUrlList(object raw)
        {
            if (raw is IEnumerable enumerable && raw is not string)
            {
                var parts = enumerable
                    .Cast<object?>()
                    .Select(ToImportCellText)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();
                return parts.Count <= 1 ? parts.FirstOrDefault() ?? string.Empty : parts;
            }

            var values = ToImportCellText(raw)
                .Split([',', '，', ';', '；', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
            return values.Count <= 1 ? values.FirstOrDefault() ?? string.Empty : values;
        }

        private static object ConvertImportTextOrJson(string text)
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

        private static object? GetImportValue(ExpandoObject data, string? field)
        {
            if (string.IsNullOrWhiteSpace(field) || field.Contains('>'))
            {
                return null;
            }

            return GetImportTopLevelValue(data, field);
        }

        private static object? GetImportTopLevelValue(ExpandoObject data, string field)
        {
            return GetDictionaryValue((IDictionary<string, object?>)data, field);
        }

        private static object? GetDictionaryValue(IDictionary<string, object?> data, string field)
        {
            return data.TryGetValue(field, out var value) ? UnwrapImportJsonValue(value) : null;
        }

        private static List<IDictionary<string, object?>> GetImportChildRows(ExpandoObject data, string parentField)
        {
            var raw = GetImportTopLevelValue(data, parentField);
            raw = UnwrapImportJsonValue(raw);
            if (raw is not IEnumerable rows || raw is string)
            {
                return [];
            }

            return rows
                .Cast<object?>()
                .Select(AsImportDictionary)
                .Where(x => x != null)
                .Cast<IDictionary<string, object?>>()
                .ToList();
        }

        private static IDictionary<string, object?>? AsImportDictionary(object? value)
        {
            value = UnwrapImportJsonValue(value);
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
                        result[key] = UnwrapImportJsonValue(entry.Value);
                    }
                }

                return result;
            }

            return null;
        }

        private static ExpandoObject CloneImportData(ExpandoObject source)
        {
            var clone = new ExpandoObject();
            var cloneDict = (IDictionary<string, object?>)clone;
            foreach (var (key, value) in (IDictionary<string, object?>)source)
            {
                cloneDict[key] = UnwrapImportJsonValue(value);
            }

            return clone;
        }

        private static void MergeImportData(ExpandoObject target, ExpandoObject source)
        {
            var targetDict = (IDictionary<string, object?>)target;
            foreach (var (key, value) in (IDictionary<string, object?>)source)
            {
                targetDict[key] = value;
            }
        }

        private static bool IsImportFieldRequired(FieldDef field)
        {
            return field.Required || field.Props.Required == true;
        }

        private static bool IsImportValueEmpty(object? value)
        {
            value = UnwrapImportJsonValue(value);
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
                return !enumerable.Cast<object?>().Any(item => !IsImportValueEmpty(item));
            }

            return false;
        }

        private static string ToImportCellText(object? value)
        {
            value = UnwrapImportJsonValue(value);
            return value switch
            {
                null => string.Empty,
                DateTime date => date.ToString("yyyy-MM-dd HH:mm:ss"),
                IEnumerable enumerable when value is not string => string.Join(",", enumerable.Cast<object?>().Select(ToImportCellText)),
                _ => value.ToString()?.Trim() ?? string.Empty,
            };
        }

        private static object? UnwrapImportJsonValue(object? value)
        {
            if (value is not JsonElement element)
            {
                return value;
            }

            return element.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt64(out var longValue) ? longValue : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Array => element.EnumerateArray().Select(item => UnwrapImportJsonValue(item)).ToList(),
                JsonValueKind.Object => element.Deserialize<ExpandoObject>(),
                _ => element.ToString(),
            };
        }

        private static string ResolveImportFieldTitle(string? field, IReadOnlyDictionary<string, FieldDef>? fieldMap)
        {
            if (string.IsNullOrWhiteSpace(field))
            {
                return string.Empty;
            }

            return fieldMap != null && fieldMap.TryGetValue(field, out var fieldDef)
                ? fieldDef.Title
                : field;
        }

        internal static List<FormDataImportFieldSnapshot> BuildImportFieldSnapshot(FormDef formDef, IReadOnlyCollection<FieldPerm>? fieldPerms = null)
        {
            var fields = new List<FormDataImportFieldSnapshot>();
            foreach (var field in formDef.Content?.Items ?? [])
            {
                AppendImportField(fields, field, null, fieldPerms);
            }

            return fields;
        }

        private static void AppendImportField(List<FormDataImportFieldSnapshot> fields, FieldDef field, FieldDef? parent, IReadOnlyCollection<FieldPerm>? fieldPerms)
        {
            if (field.Hidden || field.Type == FieldType.Signature)
            {
                return;
            }

            if (field.Type == FieldType.TableForm)
            {
                foreach (var sub in field.Columns ?? [])
                {
                    AppendImportField(fields, sub, field, fieldPerms);
                }

                return;
            }

            if (!CanImportField(field, parent, fieldPerms))
            {
                return;
            }

            fields.Add(new FormDataImportFieldSnapshot
            {
                Field = parent == null ? field.Field : $"{parent.Field}>{field.Field}",
                Title = parent == null ? field.Title : $"{parent.Title}.{field.Title}",
                Type = field.Type,
                Options = field.Props.Options,
                Required = field.Required || field.Props.Required == true,
            });
        }

        private static bool CanImportField(FieldDef field, FieldDef? parent, IReadOnlyCollection<FieldPerm>? fieldPerms)
        {
            if (fieldPerms == null)
            {
                return true;
            }

            if (parent != null)
            {
                var parentPerm = fieldPerms.FirstOrDefault(x => string.Equals(x.Id, parent.Field, StringComparison.OrdinalIgnoreCase));
                if (parentPerm is not { Visible: true, Editable: true })
                {
                    return false;
                }
            }

            var fieldKey = parent == null ? field.Field : $"{parent.Field}>{field.Field}";
            var fieldPerm = fieldPerms.FirstOrDefault(x => string.Equals(x.Id, fieldKey, StringComparison.OrdinalIgnoreCase));
            return fieldPerm is { Visible: true, Editable: true };
        }

        internal static string NormalizeFileName(string fileName)
        {
            var name = Path.GetFileName(fileName);
            foreach (var ch in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(ch, '_');
            }

            return string.IsNullOrWhiteSpace(name) ? $"import_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx" : name;
        }

        private static ExportColumnType ResolveColumnType(string key, IList<FieldDef> fields)
        {
            if (string.Equals(key, Fields.CreateTime, StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, Fields.UpdateTime, StringComparison.OrdinalIgnoreCase))
            {
                return ExportColumnType.Date;
            }

            if (string.Equals(key, Fields.FlowStatus, StringComparison.OrdinalIgnoreCase)
                || Fields.IsSystemField(key))
            {
                return ExportColumnType.String;
            }

            FieldDef? field = null;
            if (key.Contains('>'))
            {
                var parts = key.Split('>', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    var parent = fields.FirstOrDefault(x => string.Equals(x.Field, parts[0], StringComparison.OrdinalIgnoreCase));
                    field = parent?.Columns?.FirstOrDefault(x => string.Equals(x.Field, parts[1], StringComparison.OrdinalIgnoreCase));
                }
            }
            else
            {
                field = fields.FirstOrDefault(x => string.Equals(x.Field, key, StringComparison.OrdinalIgnoreCase));
            }

            return field?.Type switch
            {
                FieldType.Number => ExportColumnType.Number,
                FieldType.TimeStamp => ExportColumnType.Date,
                _ => ExportColumnType.String,
            };
        }

        private static bool IsDataSelectFieldPath(string? path, IList<FieldDef> fields)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var fieldPath = path.StartsWith("data.", StringComparison.OrdinalIgnoreCase)
                ? path[5..]
                : path;
            var parts = fieldPath.Split('>', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return false;
            }

            var field = fields.FirstOrDefault(x => string.Equals(x.Field, parts[0], StringComparison.OrdinalIgnoreCase));
            if (field == null)
            {
                return false;
            }

            if (parts.Length == 1)
            {
                return field.Type == FieldType.DataSelect;
            }

            var child = field.Columns?.FirstOrDefault(x => string.Equals(x.Field, parts[1], StringComparison.OrdinalIgnoreCase));
            return child?.Type == FieldType.DataSelect;
        }

        private static string BuildDedupKey(object source)
        {
            var json = source.SerializeToJson();
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
            return Convert.ToHexString(bytes);
        }

        private sealed record FormImportPermissionContext(
            IReadOnlyCollection<FieldPerm>? FieldPerms,
            DynamicFilter? DataScopeFilter);

        internal sealed class FormDataImportFieldSnapshot
        {
            public string Field { get; set; } = string.Empty;

            public string Title { get; set; } = string.Empty;

            public string Type { get; set; } = string.Empty;

            public List<ValueOption>? Options { get; set; }

            public bool Required { get; set; }
        }
    }
}
