using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Entities;
using EIMSNext.Core;
using EIMSNext.Core.Query;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Contracts;
using HKH.Common;
using HKH.Mef2.Integration;

namespace EIMSNext.ApiService
{
    public class FormDataApiService : ApiServiceBase<FormData, FormData, IFormDataService>
    {
        private IFormDefService _formDefService;
        private IFormDataChangeLogService _formDataChangeLogService;
        public FormDataApiService(IResolver resolver) : base(resolver)
        {
            _formDefService = resolver.Resolve<IFormDefService>();
            _formDataChangeLogService = resolver.Resolve<IFormDataChangeLogService>();
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

        private Task<long> CountExportAsync(FormDataExportRequest request)
        {
            return CountAsync(request.Filter ?? DynamicFilter.Empty);
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
    }
}
