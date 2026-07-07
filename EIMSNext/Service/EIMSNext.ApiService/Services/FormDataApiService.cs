using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Component;
using EIMSNext.Core.Entities;
using EIMSNext.Core;
using EIMSNext.Core.Query;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Contracts;
using EIMSNext.Storage.Abstractions;
using HKH.Common;
using HKH.Mef2.Integration;
using MongoDB.Bson;
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
                CanEditErrors = importLog.EditableErrorRowCount > 0,
                EditableErrorRowCount = importLog.EditableErrorRowCount,
            };
        }

        public FormDataImportEditableErrorsResponse GetEditableImportErrors(string id)
        {
            var importLog = GetAccessibleImportLog(id);
            return new FormDataImportEditableErrorsResponse
            {
                Rows = ReadEditableErrorRows(importLog),
            };
        }

        public async Task<FormDataImportRetryResponse> RetryImportAsync(string id, FormDataImportRetryRequest request)
        {
            var importLog = GetAccessibleImportLog(id);
            if (importLog.EditableErrorRowCount <= 0)
            {
                throw new InvalidOperationException("当前导入任务没有可在线修改的失败数据");
            }
            if (importLog.Status != FormDataImportStatus.CompletedWithErrors)
            {
                throw new InvalidOperationException("当前导入任务状态不允许重试");
            }

            BuildImportPermissionContext(importLog.FormId, importLog.AuthGroupId);

            var rows = request.Rows ?? [];
            if (rows.Count == 0 || rows.Count > EIMSNext.Common.Constants.FormDataImportMaxEditableErrors)
            {
                throw new ArgumentException($"重试数据不能超过 {EIMSNext.Common.Constants.FormDataImportMaxEditableErrors} 条");
            }

            var importLogService = Resolver.Resolve<IFormDataImportLogService>();
            var nextRetryCount = await importLogService.TryPrepareRetryAsync(importLog.Id, importLog.RetryCount, rows.SerializeToJson(), rows.Count)
                ?? throw new InvalidOperationException("当前导入任务状态已变化，请刷新后重试");
            await Resolver.Resolve<IMessagePublisher>().PublishAsync(new DataImportTaskArgs
            {
                ImportLogId = importLog.Id,
                CorpId = importLog.CorpId ?? string.Empty,
                RetryCount = nextRetryCount,
            });

            return new FormDataImportRetryResponse { TaskId = importLog.Id };
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
                return AndFilters(filter, CreateNoMatchFilter());
            }

            return AndFilters(filter, BuildDataScopeFilter(authGroups));
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

        private static DynamicFilter AndFilters(DynamicFilter current, DynamicFilter? additional)
        {
            if (additional == null || additional.IsEmpty)
            {
                return current;
            }

            if (current.IsGroup && current.Rel == FilterRel.And)
            {
                current.Items!.Add(additional);
                return current;
            }

            return new DynamicFilter
            {
                Rel = FilterRel.And,
                Items = [current, additional],
            };
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
                        Field = $"{Fields.CreateBy}.empId",
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
