using Asp.Versioning;
using EIMSNext.ApiCore.RateLimiting;
using EIMSNext.ApiHost.Extensions;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Cache;
using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Component;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Host.Authorization;
using EIMSNext.Service.Host.Requests;
using HKH.Mef2.Integration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EIMSNext.Service.Host.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
    [IdentityType(IdentityTypeDefaults.BusinessUser)]
    public class FormDataController(IResolver resolver) : MefControllerBase<FormDataApiService, FormData, FormData>(resolver)
    {
        private const int MongoMaxDocumentBytes = 16 * 1024 * 1024;
        private readonly IFormDefService _formDefService = resolver.Resolve<IFormDefService>();
        private readonly DataTitleResolver _dataTitleResolver = resolver.Resolve<DataTitleResolver>();
        private readonly AdminPermissionEvaluator _permissionEvaluator = resolver.Resolve<AdminPermissionEvaluator>();
        private readonly PublicFormLinkGuard _publicFormLinkGuard = resolver.Resolve<PublicFormLinkGuard>();
        private readonly PublicRateLimiter _publicRateLimiter = resolver.Resolve<PublicRateLimiter>();
        private readonly IFormDataService _formDataService = resolver.Resolve<IFormDataService>();


        /// <summary>
        /// 动态查询总数
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        [Permission(Operation = Operation.Read)]
        [IdentityType(IdentityTypeDefaults.PublicBusinessUser)]
        [PublicScope(PublicScope.QueryLink | PublicScope.FormLink)]
        [HttpPost("dynamic/$count")]
        public ActionResult GetDynamicCount([FromBody] DynamicFindOptions<FormData> options)
        {
            var filtered = FilterResult(new DynamicFindOptions<FormData>
            {
                Filter = options.Filter,
                Keyword = options.Keyword,
                SearchFields = options.SearchFields,
                Scope = options.Scope,
                IncludeDeleted = options.IncludeDeleted,
            });
            return Ok(ApiService.Count(filtered.Filter ?? DynamicFilter.Empty));
        }
        /// <summary>
        /// 动态查询数据
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        [Permission(Operation = Operation.Read)]
        [IdentityType(IdentityTypeDefaults.PublicBusinessUser)]
        [PublicScope(PublicScope.QueryLink | PublicScope.FormLink)]
        [HttpPost("dynamic/$query")]
        public ActionResult GetDynamicData([FromBody] DynamicFindOptions<FormData> options)
        {
            var filtered = FilterResult(options);
            var result = ApiService.Find(filtered).ToList();
            return Ok(new { value = result.Select(item => ToViewModel(item)) });
        }

        /// <summary>
        /// 动态查询总数
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        [Permission(Operation = Operation.Read)]
        [IdentityType(IdentityTypeDefaults.PublicBusinessUser)]
        [PublicScope(PublicScope.QueryLink | PublicScope.FormLink)]
        [HttpPost("$count")]
        public ActionResult GetCount([FromBody] DynamicFilter filter)
        {
            var options = FilterResult(new DynamicFindOptions<FormData> { Filter = filter });
            return Ok(ApiService.Count(options.Filter ?? DynamicFilter.Empty));
        }

        /// <summary>
        /// 动态查询数据
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        [Permission(Operation = Operation.Read)]
        [IdentityType(IdentityTypeDefaults.PublicBusinessUser)]
        [PublicScope(PublicScope.QueryLink | PublicScope.FormLink)]
        [HttpPost("$query")]
        public ActionResult GetData([FromBody] DynamicFindOptions<FormData> options)
        {
            if (options.Select == null || options.Select.Count == 0)
            {
                //不指定列时，不返回历史日志字段
                options.Select = new DynamicFieldList()
                {
                    DynamicField.Create("updateLog",false),
                    DynamicField.Create("changeLog",false)
                };
            }
            var filtered = FilterResult(options);
            var result = ApiService.Find(filtered).ToList();
            return Ok(new { value = result.Select(item => ToViewModel(item)) });
        }

        [Permission(Operation = Operation.Read)]
        [IdentityType(IdentityTypeDefaults.PublicBusinessUser)]
        [PublicScope(PublicScope.QueryLink | PublicScope.FormLink)]
        [HttpPost("filter/options")]
        public async Task<ActionResult> GetFilterOptions([FromBody] FormDataFilterOptionsRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FormId) || string.IsNullOrWhiteSpace(request.Field))
            {
                return BadRequest();
            }

            var formDef = _formDefService.Get(request.FormId);
            if (formDef != null && IsDataSelectFieldPath(request.Field, formDef.Content?.Items ?? []))
            {
                return BadRequest("数据选择字段不能用于过滤");
            }

            var result = await ApiService.GetFilterOptionsAsync(request);
            return Ok(result);
        }

        [Permission(Operation = Operation.Read)]
        [HttpPost("Export")]
        public async Task<ActionResult> Export([FromBody] FormDataExportRequest request)
        {
            return Ok(await ApiService.ExportAsync(FilterResult(request)));
        }

        [Permission(ResourceCode = Resources.FormData, Operation = Operation.Import)]
        [Consumes("multipart/form-data")]
        [HttpPost("Import/Preview")]
        public ActionResult PreviewImport([FromForm] FormDataImportPreviewRequest request)
        {
            if (request.File == null || request.File.Length == 0 || string.IsNullOrWhiteSpace(request.FormId))
            {
                return BadRequest();
            }

            using var stream = request.File.OpenReadStream();
            return Ok(ApiResult.Success(ApiService.PreviewImport(request.FormId, stream, request.File.FileName, request.File.Length)));
        }

        [Permission(ResourceCode = Resources.FormData, Operation = Operation.Import)]
        [Consumes("multipart/form-data")]
        [HttpPost("Import")]
        public async Task<ActionResult> Import([FromForm] FormDataImportRequest request)
        {
            if (request.File == null || request.File.Length == 0 || string.IsNullOrWhiteSpace(request.Options))
            {
                return BadRequest();
            }

            var importRequest = request.Options.DeserializeFromJson<FormDataImportStartRequest>();
            if (importRequest == null)
            {
                return BadRequest();
            }

            await using var stream = request.File.OpenReadStream();
            return Ok(ApiResult.Success(await ApiService.StartImportAsync(importRequest, stream, request.File.FileName, request.File.Length)));
        }

        [Permission(ResourceCode = Resources.FormData, Operation = Operation.Import)]
        [HttpGet("Import/{id}")]
        public ActionResult GetImportStatus([FromRoute] string id)
        {
            return Ok(ApiResult.Success(ApiService.GetImportStatus(id)));
        }

        [Permission(ResourceCode = Resources.FormData, Operation = Operation.Import)]
        [HttpGet("Import/{id}/Errors")]
        public ActionResult GetImportErrors([FromRoute] string id)
        {
            return Ok(ApiResult.Success(ApiService.GetEditableImportErrors(id)));
        }

        [Permission(ResourceCode = Resources.FormData, Operation = Operation.Import)]
        [HttpPost("Import/{id}/Retry")]
        public async Task<ActionResult> RetryImport([FromRoute] string id, [FromBody] FormDataImportRetryRequest request)
        {
            return Ok(ApiResult.Success(await ApiService.RetryImportAsync(id, request)));
        }

        [Permission(ResourceCode = Resources.FormData, Operation = Operation.Delete)]
        [HttpPost("manage/restore")]
        public async Task<ActionResult> Restore([FromBody] FormDataManageRequest request)
        {
            var keys = request.Keys?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [];
            if (keys.Count == 0)
            {
                return BadRequest();
            }

            await _formDataService.RestoreAsync(FilterManageableIds(keys));
            return NoContent();
        }

        [Permission(ResourceCode = Resources.FormData, Operation = Operation.Delete)]
        [HttpDelete("manage/purge")]
        public async Task<ActionResult> Purge([FromBody] FormDataManageRequest request)
        {
            var keys = request.Keys?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [];
            if (keys.Count == 0)
            {
                return BadRequest();
            }

            await _formDataService.PurgeAsync(FilterManageableIds(keys));
            return NoContent();
        }

        /// <summary>
        /// 对按请求的上下文进行数据过滤，比如用户只能访问被授权的数据
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        protected virtual DynamicFindOptions<FormData> FilterResult(DynamicFindOptions<FormData> query)
        {
            query.Take = query.GetEffectiveTake();
            query.Skip = query.GetEffectiveSkip();
            query = FilterByCorpId(query);
            if (!query.IncludeDeleted)
            {
                query = FilterByDeleted(query);
            }
            query = FilterBySearch(query);
            query = RejectUnsupportedFilterFields(query);
            query = FilterByUnsupportedSortFields(query);
            return FilterByPermission(query);
        }
        protected DynamicFindOptions<FormData> FilterByDeleted(DynamicFindOptions<FormData> query)
        {
            query.Filter = query.Filter.And(Fields.DeleteFlag, FilterOp.Ne, true);
            return query;
        }
        protected DynamicFindOptions<FormData> FilterByCorpId(DynamicFindOptions<FormData> query)
        {
            query.Filter = query.Filter.And(Fields.CorpId, FilterOp.Eq, IdentityContext.CurrentCorpId);
            return query;
        }
        protected virtual DynamicFindOptions<FormData> FilterByPermission(DynamicFindOptions<FormData> query)
        {
            if (IdentityContext.IdentityType == IdentityType.Public)
            {
                var validator = Resolver.Resolve<IPublicAccessValidator>();
                var formId = query.Scope?.FormId ?? FindFormId(query.Filter);
                query.Filter = RestrictPublicQueryFilter(validator, formId, query.Filter);
                query.Filter = string.IsNullOrWhiteSpace(formId)
                    ? CreateNoMatchFilter()
                    : validator.ApplyFormDataScope(formId!, query.Filter);
                query.Scope = null;
                ApplyPublicProjection(validator, formId, query);
                query.Take = Math.Clamp(query.GetEffectiveTake(), 1, 200);
                query.Skip = Math.Max(0, query.Skip);
                return query;
            }

            if (query.Scope != null)
            {
                if (query.Scope.InheritMemberPermissions && !string.IsNullOrWhiteSpace(query.Scope.FormId))
                {
                    if (_permissionEvaluator.HasUnrestrictedManagementIdentity)
                    {
                        return query;
                    }

                    query.Filter = query.Filter.And(BuildInheritedPermissionFilter(query.Scope.FormId!));
                    return query;
                }

                if (!string.IsNullOrEmpty(query.Scope.AuthGroupId))
                {
                    var authGrp = Resolver.GetService<AuthGroup>().Get(query.Scope.AuthGroupId);
                    if (authGrp != null)
                    {
                        query.Filter = query.Filter.And(BuildAuthGroupDataFilter(authGrp));
                    }
                }
            }

            return query;
        }

        protected virtual DynamicFindOptions<FormData> FilterBySearch(DynamicFindOptions<FormData> query)
        {
            var formId = query.Scope?.FormId ?? FindFormId(query.Filter);
            query.Filter = query.Filter.And(BuildSearchFilter(formId, query.Keyword, query.SearchFields));
            return query;
        }

        protected virtual FormDataExportRequest FilterResult(FormDataExportRequest request)
        {
            request = FilterByCorpId(request);
            if (!request.IncludeDeleted)
            {
                request = FilterByDeleted(request);
            }
            request = FilterBySearch(request);
            request = RejectUnsupportedFilterFields(request);
            return FilterByPermission(request);
        }

        private DynamicFindOptions<FormData> RejectUnsupportedFilterFields(DynamicFindOptions<FormData> query)
        {
            var formId = query.Scope?.FormId ?? FindFormId(query.Filter);
            var formDef = string.IsNullOrWhiteSpace(formId) ? null : _formDefService.Get(formId);
            if (formDef != null && ContainsDataSelectFilter(query.Filter, formDef.Content?.Items ?? []))
            {
                throw new BadRequestException("数据选择字段不能用于过滤");
            }

            return query;
        }

        private FormDataExportRequest RejectUnsupportedFilterFields(FormDataExportRequest request)
        {
            var formDef = string.IsNullOrWhiteSpace(request.FormId) ? null : _formDefService.Get(request.FormId);
            if (formDef != null && ContainsDataSelectFilter(request.Filter, formDef.Content?.Items ?? []))
            {
                throw new BadRequestException("数据选择字段不能用于过滤");
            }

            return request;
        }

        private static bool ContainsDataSelectFilter(DynamicFilter? filter, IList<FieldDef> fields)
        {
            if (filter == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(filter.Field))
            {
                return IsDataSelectFieldPath(filter.Field, fields);
            }

            return filter.Items?.Any(item => ContainsDataSelectFilter(item, fields)) == true;
        }

        protected virtual FormDataExportRequest FilterByPermission(FormDataExportRequest request)
        {
            if (IdentityContext.IdentityType == IdentityType.Public)
            {
                request.Filter = CreateNoMatchFilter();
                return request;
            }

            if (!string.IsNullOrEmpty(request.AuthGroupId))
            {
                var authGrp = Resolver.GetService<AuthGroup>().Get(request.AuthGroupId);
                if (authGrp != null)
                {
                    var filter = request.Filter;
                    if (filter == null) { filter = new DynamicFilter(); }

                    if (authGrp.Type == AuthGroupType.ManageSelfData)
                    {
                        if (filter.IsGroup && filter.Rel == FilterRel.And)
                        {
                            filter.Items!.Add(new DynamicFilter() { Field = $"{Fields.CreateBy}.empId", Op = FilterOp.Eq, Value = IdentityContext.CurrentEmployee!.Id });
                        }
                        else
                        {
                            filter = new DynamicFilter() { Rel = FilterRel.And, Items = [new DynamicFilter() { Field = $"{Fields.CreateBy}.empId", Op = FilterOp.Eq, Value = IdentityContext.CurrentEmployee!.Id }, filter] };
                        }
                    }
                    else if (authGrp.Type == AuthGroupType.Custom)
                    {
                        if (!string.IsNullOrEmpty(authGrp.DataFilter))
                        {
                            var condList = authGrp.DataFilter.DeserializeFromJson<ConditionList>();
                            if (condList != null)
                            {
                                var dataFilter = condList.ToDynamicFilter();

                                if (filter.IsGroup && filter.Rel == FilterRel.And)
                                {
                                    filter.Items!.Add(dataFilter);
                                }
                                else
                                {
                                    filter = new DynamicFilter() { Rel = FilterRel.And, Items = [dataFilter, filter] };
                                }
                            }
                        }
                    }

                    request.Filter = filter;
                }
            }

            return request;
        }

        protected virtual FormDataExportRequest FilterBySearch(FormDataExportRequest request)
        {
            request.Filter = request.Filter.And(BuildSearchFilter(request.FormId, request.Keyword, request.SearchFields));
            return request;
        }

        protected virtual FormDataExportRequest FilterByDeleted(FormDataExportRequest request)
        {
            request.Filter = request.Filter.And(Fields.DeleteFlag, FilterOp.Ne, true);
            return request;
        }

        protected virtual FormDataExportRequest FilterByCorpId(FormDataExportRequest request)
        {
            request.Filter = request.Filter.And(Fields.CorpId, FilterOp.Eq, IdentityContext.CurrentCorpId);
            return request;
        }

        /// <summary>
        /// 查询表单数据修改日志
        /// </summary>
        /// <param name="key">表单数据ID</param>
        /// <param name="skip">跳过数量</param>
        /// <param name="top">返回数量</param>
        /// <param name="authGroupId">授权组ID</param>
        /// <returns></returns>
        [Permission(Operation = Operation.Read)]
        [HttpGet("{key}/changelog")]
        public ActionResult GetChangeLogs([FromRoute] string key, [FromQuery] int skip = 0, [FromQuery] int top = 20, [FromQuery] string? authGroupId = null)
        {
            if (!CanReadFormData(key, authGroupId)) return NotFound();

            return Ok(new { value = ApiService.GetChangeLogs(key, skip, top) });
        }

        /// <summary>
        /// 查询表单数据修改日志总数
        /// </summary>
        /// <param name="key">表单数据ID</param>
        /// <param name="authGroupId">授权组ID</param>
        /// <returns></returns>
        [Permission(Operation = Operation.Read)]
        [HttpGet("{key}/changelog/$count")]
        public ActionResult GetChangeLogCount([FromRoute] string key, [FromQuery] string? authGroupId = null)
        {
            if (!CanReadFormData(key, authGroupId)) return NotFound();

            return Ok(ApiService.CountChangeLogs(key));
        }

        private bool CanReadFormData(string key, string? authGroupId)
        {
            var options = new DynamicFindOptions<FormData>
            {
                Filter = new DynamicFilter { Field = Fields.BsonId, Op = FilterOp.Eq, Value = key },
                Select = new DynamicFieldList { DynamicField.Create(Fields.Id, true) },
                Take = 1,
                Scope = string.IsNullOrWhiteSpace(authGroupId) ? null : new DataScope { AuthGroupId = authGroupId }
            };

            return ApiService.Find(FilterResult(options)).FirstOrDefault() != null;
        }

        [Permission(Operation = Operation.Read)]
        [HttpGet("{key}/permission-scope")]
        public ActionResult GetPermissionScope([FromRoute] string key, [FromQuery] string formId)
        {
            if (string.IsNullOrWhiteSpace(formId))
            {
                return BadRequest();
            }

            var data = ApiService.Find(FilterResult(new DynamicFindOptions<FormData>
            {
                Filter = new DynamicFilter { Field = Fields.BsonId, Op = FilterOp.Eq, Value = key },
                Select = new DynamicFieldList { DynamicField.Create(Fields.Id, true), DynamicField.Create("formId", true) },
                Take = 1,
            })).FirstOrDefault();
            if (data == null)
            {
                return NotFound();
            }

            if (!string.Equals(data.FormId, formId, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest();
            }

            if (_permissionEvaluator.HasUnrestrictedManagementIdentity)
            {
                return Ok(new FormDataPermissionScopeResponse
                {
                    DataPerms = DataPerms.All,
                    FieldPerms = null,
                });
            }

            var matchedGroups = _permissionEvaluator.GetUsageAuthGroupsForCurrentEmployee(formId)
                .Where(HasInheritedDataAccess)
                .ToList();
            if (matchedGroups.Count == 0)
            {
                return Ok(new FormDataPermissionScopeResponse
                {
                    DataPerms = DataPerms.None,
                    FieldPerms = [],
                });
            }

            var applicableGroups = matchedGroups
                .Where(group => AuthGroupAppliesToData(group, key))
                .ToList();
            if (applicableGroups.Count == 0)
            {
                return Ok(new FormDataPermissionScopeResponse
                {
                    DataPerms = DataPerms.None,
                    FieldPerms = [],
                });
            }

            var mergedPerms = applicableGroups
                .Select(GetEffectiveDataPerms)
                .Aggregate(DataPerms.None, (current, next) => current | next);

            var mergedFieldPerms = MergeFieldPerms(applicableGroups);

            return Ok(new FormDataPermissionScopeResponse
            {
                DataPerms = mergedPerms,
                FieldPerms = mergedFieldPerms,
            });
        }

        /// <summary>
        /// 单条查询
        /// </summary>
        /// <param name="key"></param>
        /// <param name="select"></param>
        /// <returns></returns>
        [Permission(Operation = Operation.Read)]
        [IdentityType(IdentityTypeDefaults.PublicBusinessUser)]
        [PublicScope(PublicScope.DataLink)]
        [HttpGet("{key}")]
        public ActionResult Get([FromRoute] string key, [FromQuery] string? select)
        {
            FormData? publicData = null;
            if (IdentityContext.IdentityType == IdentityType.Public)
            {
                publicData = ApiService.Get(key);
                if (publicData == null)
                {
                    return NotFound();
                }

                var validator = Resolver.Resolve<IPublicAccessValidator>();
                if (!validator.CanReadFormData(publicData.FormId) && !validator.CanReadDashboardForm(publicData.FormId))
                {
                    return NotFound();
                }
            }

            var fields = new DynamicFieldList();
            if (!string.IsNullOrEmpty(select))
            {
                foreach (var field in select.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    fields.Add(new DynamicField { Field = field, Visible = true });
                }
            }

            var queryOptions = new DynamicFindOptions<FormData>()
            {
                Select = fields,
                Filter = new DynamicFilter { Field = "_id", Op = FilterOp.Eq, Value = key }
            };
            if (IdentityContext.IdentityType != IdentityType.Public)
            {
                var accessProbe = ApiService.Find(FilterResult(new DynamicFindOptions<FormData>
                {
                    Select = new DynamicFieldList { DynamicField.Create(Fields.FormId, true) },
                    Filter = new DynamicFilter { Field = Fields.BsonId, Op = FilterOp.Eq, Value = key },
                    Take = 1,
                })).FirstOrDefault();
                if (accessProbe == null)
                {
                    return NotFound();
                }

                queryOptions.Scope = new DataScope
                {
                    FormId = accessProbe.FormId,
                    InheritMemberPermissions = true,
                };
            }
            if (IdentityContext.IdentityType == IdentityType.Public)
            {
                queryOptions.Scope = new DataScope { FormId = publicData!.FormId };
                queryOptions.Select = BuildPublicSelectList(ResolvePublicSingleViewFields(publicData.FormId));
            }

            var options = FilterResult(queryOptions);
            var result = ApiService.Find(options).FirstOrDefault();
            if (result == null)
            {
                return NotFound();
            }

            return Ok(ToViewModel(result, publicData != null ? _formDefService.Get(publicData.FormId) : null));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost]
        [Permission(ResourceCode = Resources.FormData, Operation = Operation.Add)]
        [IdentityType(IdentityTypeDefaults.PublicBusinessUser)]
        [PublicScope(PublicScope.FormLink)]
        public async Task<IActionResult> Post([FromBody] FormDataRequest model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState.ToErrorString());
            }

            FormData entity = model.CastTo<FormDataRequest, FormData>();
            if (IdentityContext.IdentityType == IdentityType.Public)
            {
                var validator = Resolver.Resolve<IPublicAccessValidator>();
                if (!validator.CanSubmitForm(entity.FormId))
                {
                    return Forbid();
                }

                if (!ValidatePublicWechatOpenId(validator, entity, model.Action, out var message))
                {
                    return BadRequest(message);
                }

                var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var rate = await _publicRateLimiter.CheckAsync("submit", entity.FormId, ip);
                if (!rate.Allowed)
                {
                    return StatusCode(StatusCodes.Status429TooManyRequests, new { error = "rate_limited", limit = rate.Limit, window = (int)rate.Window.TotalSeconds });
                }

                var setting = validator.GetCurrentSetting();
                if (setting?.TargetType == PublicTargetType.Form)
                {
                    var wxOpenId = TryGetWxOpenId(entity);
                    try
                    {
                        _publicFormLinkGuard.EnsureCanSubmit(setting.Form.FormLink, entity, wxOpenId, ip, setting.CorpId, setting.TargetId);
                    }
                    catch (PublicOneSubmitDuplicateException ex)
                    {
                        return Conflict(new { error = "already_submitted", message = ex.Message });
                    }
                }
            }

            //默认草稿
            entity.FlowStatus = FlowStatus.Draft;

            if (!IsMongoDocumentSizeAllowed(entity))
            {
                return BadRequest("表单数据不能超过 16MB");
            }

            //if (!ValidateData(entity, null, out ApiResult? fail))
            //    return BadRequest(fail?.Message);

            await ApiService.AddAsync(entity, model.Action);
            return Ok(ToViewModel(entity));
        }

        private static string? TryGetWxOpenId(FormData entity)
        {
            if (entity.Data is IDictionary<string, object?> data &&
                data.TryGetValue(PublicFormSystemFieldHelper.WxOpenId, out var value) &&
                value != null &&
                !string.IsNullOrWhiteSpace(value.ToString()))
            {
                return value.ToString();
            }
            return null;
        }

        private static bool ValidatePublicWechatOpenId(IPublicAccessValidator validator, FormData entity, DataAction action, out string message)
        {
            message = string.Empty;
            if (action != DataAction.Submit)
            {
                return true;
            }

            var setting = validator.GetCurrentSetting();
            if (setting?.TargetType != PublicTargetType.Form ||
                setting.Form.FormLink.Wechat.Enabled != true)
            {
                return true;
            }

            var data = entity.Data as IDictionary<string, object?>;
            if (data != null &&
                data.TryGetValue(PublicFormSystemFieldHelper.WxOpenId, out var value) &&
                value != null &&
                !string.IsNullOrWhiteSpace(value.ToString()))
            {
                return true;
            }

            message = "微信 OpenId 不能为空";
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        [Permission(ResourceCode = Resources.FormData, Operation = Operation.Edit)]
        [HttpPut("{key}")]
        public async Task<IActionResult> Put([FromRoute] string key, [FromBody] FormDataRequest model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState.ToErrorString());
            }
            //根据key获取数据库中的实体
            FormData? entity = ApiService.Get(key);
            if (entity == null) return NotFound();

            ServiceContext.ScopeCache.Set(entity.Id, entity.DeepClone(), DataVersion.Old);

            //保存原始实体的重要字段
            var originalCorpId = entity.CorpId;
            var originalAppId = entity.AppId;
            var originalFormId = entity.FormId;
            var originalDeleteFlag = entity.DeleteFlag;
            if (!ValidateFormDataScope(entity, model, out var scopeError))
            {
                return BadRequest(scopeError);
            }

            //将请求的数据直接复制到原始实体，而不是通过中间转换
            model.CopyTo(entity);

            //恢复重要字段，确保不会丢失
            entity.CorpId = originalCorpId;
            entity.AppId = originalAppId;
            entity.FormId = originalFormId;
            entity.DeleteFlag = originalDeleteFlag;


            if (key != entity.Id)
            {
                return BadRequest("请求修改对象的Key不一致");
            }

            if (!IsMongoDocumentSizeAllowed(entity))
            {
                return BadRequest("表单数据不能超过 16MB");
            }

            //if (!ValidateData(entity, null, out ApiResult? fail))
            //    return BadRequest(fail?.Message);

            await ApiService.ReplaceAsync(entity, model.Action);
            return Ok(ToViewModel(entity));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="delta"></param>
        /// <returns></returns>
        [Permission(ResourceCode = Resources.FormData, Operation = Operation.Edit)]
        [HttpPatch("{key}")]
        public async Task<IActionResult> Patch([FromRoute] string key, [FromBody] Delta<FormDataRequest> delta)
        {
            if (delta == null)
            {
                return BadRequest("数据解析失败，请检查数据格式, 确认正确的字段名和数据类型");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState.ToErrorString());
            }
            else if (TryGetId(delta, out string sId) && key != sId)
            {
                return BadRequest("请求修改对象的Key不一致");
            }

            FormData? entity = ApiService.Get(key);
            if (entity == null) return NotFound();

            ServiceContext.ScopeCache.Set(entity.Id, entity.DeepClone(), DataVersion.Old);

            FormDataRequest model = entity.CastTo<FormData, FormDataRequest>();

            delta.Patch(model);
            if (!ValidateFormDataScope(entity, model, out var scopeError))
            {
                return BadRequest(scopeError);
            }

            model.CopyTo(entity);

            if (!IsMongoDocumentSizeAllowed(entity))
            {
                return BadRequest("表单数据不能超过 16MB");
            }

            //if (!ValidateData(entity, delta, out ApiResult? fail))
            //    return BadRequest(fail?.Message);

            // 透传 body 中的 action：缺省视为 Save。流水号、提交流程等仅在 Submit 时触发，
            // 旧实现漏传 action 导致 PATCH 永远走 Save，流水号不会生成。
            await ApiService.ReplaceAsync(entity, model.Action);

            return Ok(ToViewModel(entity));
        }

        private static bool ValidateFormDataScope(FormData entity, FormDataRequest model, out string message)
        {
            message = string.Empty;
            if (!string.Equals(entity.AppId, model.AppId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(entity.FormId, model.FormId, StringComparison.OrdinalIgnoreCase))
            {
                message = "请求修改对象的应用或表单不一致";
                return false;
            }

            return true;
        }

        private static bool IsMongoDocumentSizeAllowed(FormData entity)
        {
            return EntityExtension.ToBson(entity).ToBson().Length <= MongoMaxDocumentBytes;
        }

        /// <summary>
        /// 删除，支持两种方式 1.请求地址中key != batch, 则删除指定对象 2.请求地址中key == batch, 请求体中keys = 1,2,3...可以批量删除
        /// </summary>
        /// <param name="key">主键Id</param>
        /// <param name="batch">批量删除</param>
        /// <returns></returns>
        [Permission(ResourceCode = Resources.FormData, Operation = Operation.Delete)]
        [HttpDelete("{key}")]
        public async Task<ActionResult> Delete([FromRoute] string key, [FromBody] DeleteBatch? batch)
        {
            if ("batch".EqualsIgnoreCase(key))
            {
                if (batch?.Keys?.Count > 0)
                {
                    await ApiService.DeleteAsync(batch.Keys);
                }
                else
                {
                    return BadRequest();
                }
            }
            else
            {
                await ApiService.DeleteAsync(key);
            }
            return NoContent();
        }

        private DynamicFilter BuildInheritedPermissionFilter(string formId)
        {
            var authGroups = _permissionEvaluator.GetUsageAuthGroupsForCurrentEmployee(formId)
                .Where(HasInheritedDataAccess)
                .ToList();
            if (authGroups.Count == 0)
            {
                return CreateNoMatchFilter();
            }

            var rangeFilters = new List<DynamicFilter>();
            foreach (var authGroup in authGroups)
            {
                var groupFilter = BuildAuthGroupDataFilter(authGroup);
                if (groupFilter == null || groupFilter.IsEmpty)
                {
                    return DynamicFilter.Empty;
                }

                rangeFilters.Add(groupFilter);
            }

            return OrFilters(rangeFilters) ?? CreateNoMatchFilter();
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

        private DynamicFilter? RestrictPublicQueryFilter(IPublicAccessValidator validator, string? formId, DynamicFilter? filter)
        {
            if (string.IsNullOrWhiteSpace(formId) || IsPublicSingleReadRequest())
            {
                return filter;
            }

            var setting = validator.GetCurrentSetting();
            if (setting?.TargetType != PublicTargetType.Form)
            {
                return filter;
            }

            if (validator.IsRelatedForm(formId))
            {
                return filter;
            }

            var allowedFields = new HashSet<string>(setting.Form.QueryLink.QueryFields ?? [], StringComparer.OrdinalIgnoreCase);
            return IsFilterAllowed(filter, allowedFields) ? filter : CreateNoMatchFilter();
        }

        private static bool IsFilterAllowed(DynamicFilter? filter, IReadOnlySet<string> allowedFields)
        {
            if (filter == null || filter.IsEmpty)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(filter.Field))
            {
                return IsPublicFilterFieldAllowed(filter.Field, allowedFields);
            }

            return filter.Items?.All(item => IsFilterAllowed(item, allowedFields)) != false;
        }

        private static bool IsPublicFilterFieldAllowed(string field, IReadOnlySet<string> allowedFields)
        {
            if (field.Equals(Fields.FormId, StringComparison.OrdinalIgnoreCase) ||
                field.Equals(Fields.CorpId, StringComparison.OrdinalIgnoreCase) ||
                field.Equals(Fields.DeleteFlag, StringComparison.OrdinalIgnoreCase) ||
                field.Equals(Fields.BsonId, StringComparison.OrdinalIgnoreCase) ||
                field.Equals(Fields.Id, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!field.StartsWith($"{Fields.Data}.", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var dataField = field[$"{Fields.Data}.".Length..];
            var root = dataField.Split('.', 2)[0];
            return allowedFields.Count > 0 && allowedFields.Contains(root);
        }

        private void ApplyPublicProjection(IPublicAccessValidator validator, string? formId, DynamicFindOptions<FormData> query)
        {
            if (string.IsNullOrWhiteSpace(formId) || IsPublicCountRequest())
            {
                return;
            }

            var setting = validator.GetCurrentSetting();
            if (setting?.TargetType != PublicTargetType.Form)
            {
                return;
            }

            if (validator.IsRelatedForm(formId))
            {
                return;
            }

            query.Select = BuildPublicSelectList(IsPublicSingleReadRequest()
                ? ResolvePublicSingleViewFields(formId)
                : ResolvePublicListFields(formId));
        }

        private IEnumerable<string> ResolvePublicSingleViewFields(string formId)
        {
            var validator = Resolver.Resolve<IPublicAccessValidator>();
            var setting = validator.GetCurrentSetting();
            var visible = setting?.Form.DataLink.Fields?
                .Where(x => x.Visible)
                .Select(x => x.Field)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            return visible?.Count > 0 ? visible : ResolveOrdinaryFields(formId);
        }

        private IEnumerable<string> ResolvePublicListFields(string formId)
        {
            var validator = Resolver.Resolve<IPublicAccessValidator>();
            var setting = validator.GetCurrentSetting();
            var display = setting?.Form.QueryLink.DisplayFields?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            return display?.Count > 0 ? display : ResolveOrdinaryFields(formId).Take(5);
        }

        private IEnumerable<string> ResolveOrdinaryFields(string formId)
        {
            var form = _formDefService.Get(formId);
            return FlattenOrdinaryFields(form?.Content?.Items ?? []);
        }

        private static IEnumerable<string> FlattenOrdinaryFields(IEnumerable<FieldDef> fields)
        {
            foreach (var field in fields)
            {
                var publicSystemField = IsPublicSystemField(field);
                if ((!publicSystemField && field.Hidden) || IsOrgField(field.Type))
                {
                    continue;
                }

                if (field.Type == FieldType.TableForm && field.Columns?.Count > 0)
                {
                    foreach (var sub in FlattenOrdinaryFields(field.Columns))
                    {
                        yield return $"{field.Field}>{sub}";
                    }
                    continue;
                }

                yield return field.Field;
            }
        }

        private static bool IsPublicSystemField(FieldDef field)
        {
            return PublicFormSystemFieldHelper.IsPublicSystemField(field.Field) &&
                   (string.Equals(field.Source, PublicFormSystemFieldHelper.Source, StringComparison.OrdinalIgnoreCase) ||
                    PublicFormSystemFieldHelper.IsPublicSystemField(field.SystemKind));
        }

        private static bool IsOrgField(string? type)
        {
            return type == FieldType.Department1 ||
                   type == FieldType.Department2 ||
                   type == FieldType.Employee1 ||
                   type == FieldType.Employee2;
        }

        private static DynamicFieldList BuildPublicSelectList(IEnumerable<string> fields)
        {
            var select = new DynamicFieldList
            {
                DynamicField.Create(Fields.Id),
                DynamicField.Create(Fields.AppId),
                DynamicField.Create(Fields.FormId),
                DynamicField.Create(Fields.DataTitle),
                DynamicField.Create(Fields.CreateTime)
            };

            foreach (var field in fields.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                select.Add(DynamicField.Create($"{Fields.Data}.{field}"));
            }

            return select;
        }

        private bool IsPublicSingleReadRequest()
        {
            return HttpContext.Request.Method.Equals(HttpMethods.Get, StringComparison.OrdinalIgnoreCase) &&
                   HttpContext.Request.RouteValues.TryGetValue("key", out _);
        }

        private bool IsPublicCountRequest()
        {
            return HttpContext.Request.Path.Value?.Contains("$count", StringComparison.OrdinalIgnoreCase) == true;
        }

        private static string? FindFormId(DynamicFilter? filter)
        {
            if (filter == null || filter.IsEmpty)
            {
                return null;
            }

            if (string.Equals(filter.Field, Fields.FormId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(filter.Op, FilterOp.Eq, StringComparison.OrdinalIgnoreCase))
            {
                return filter.Value?.ToString();
            }

            if (filter.Items == null)
            {
                return null;
            }

            foreach (var item in filter.Items)
            {
                var formId = FindFormId(item);
                if (!string.IsNullOrWhiteSpace(formId))
                {
                    return formId;
                }
            }

            return null;
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

        private List<string> FilterManageableIds(IEnumerable<string> ids)
        {
            var requested = ids
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (requested.Count == 0)
            {
                return [];
            }

            var query = FilterResult(new DynamicFindOptions<FormData>
            {
                IncludeDeleted = true,
                Filter = new DynamicFilter
                {
                    Field = Fields.BsonId,
                    Op = FilterOp.In,
                    Value = requested.Cast<object>().ToList(),
                },
                Select = new DynamicFieldList { DynamicField.Create(Fields.Id, true) },
                Take = requested.Count,
            });

            return ApiService.Find(query).ToList().Select(x => x.Id).ToList();
        }

        private Dictionary<string, string> BuildSearchableFieldMap(FormDef formDef)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [Fields.DataTitle] = FieldType.Input
            };

            foreach (var item in formDef.Content?.Items ?? [])
            {
                AppendSearchableField(map, item, null);
            }

            return map;
        }

        private void AppendSearchableField(Dictionary<string, string> map, FieldDef field, FieldDef? parent)
        {
            if (field.Type == FieldType.TableForm)
            {
                foreach (var sub in field.Columns ?? [])
                {
                    AppendSearchableField(map, sub, field);
                }
                return;
            }

            if (!IsSearchableFieldType(field.Type))
            {
                return;
            }

            var logicalField = parent == null ? field.Field : $"{parent.Field}>{field.Field}";
            map[logicalField] = field.Type;
        }

        private static bool IsSearchableFieldType(string? type)
        {
            return type == FieldType.Input
                || type == FieldType.TextArea
                || type == FieldType.Number
                || type == FieldType.Radio
                || type == FieldType.CheckBox
                || type == FieldType.Select1
                || type == FieldType.Select2
                || type == FieldType.SerialNo;
        }

        private DynamicFindOptions<FormData> FilterByUnsupportedSortFields(DynamicFindOptions<FormData> query)
        {
            if (query.Sort == null || query.Sort.Count == 0)
            {
                return query;
            }

            var formId = query.Scope?.FormId ?? FindFormId(query.Filter);
            var formDef = string.IsNullOrWhiteSpace(formId) ? null : _formDefService.Get(formId);
            if (formDef == null)
            {
                return query;
            }

            var fields = formDef.Content?.Items ?? [];
            var allowedSort = new DynamicSortList();
            allowedSort.AddRange(query.Sort.Where(sort => !IsDataSelectFieldPath(sort.Field, fields)));
            query.Sort = allowedSort;
            return query;
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

        private DynamicFilter? BuildSearchFilter(string? formId, string? keyword, IEnumerable<string>? searchFields)
        {
            var normalizedKeyword = keyword?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedKeyword))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(formId))
            {
                return CreateNoMatchFilter();
            }

            var formDef = _formDefService.Get(formId);
            if (formDef == null)
            {
                return CreateNoMatchFilter();
            }

            var searchableMap = BuildSearchableFieldMap(formDef);
            var requestedFields = (searchFields ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var actualFields = requestedFields.Count > 0
                ? requestedFields.Where(field => searchableMap.ContainsKey(field)).ToList()
                : searchableMap.Keys.ToList();

            if (actualFields.Count == 0)
            {
                return CreateNoMatchFilter();
            }

            return OrFilters(actualFields.Select(field => new DynamicFilter
            {
                Field = Fields.IsSystemField(field) ? field : $"data.{field}",
                Type = searchableMap[field],
                Op = FilterOp.Text,
                Value = normalizedKeyword,
            }));
        }

        private FormDataViewModel ToViewModel(FormData formData, FormDef? formDef = null)
        {
            formDef ??= _formDefService.Get(formData.FormId);
            var dataTitle = formDef == null ? null : _dataTitleResolver.ResolveDataTitle(formData, formDef);
            return FormDataViewModel.FromFormData(formData, dataTitle);
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

        private static bool HasInheritedDataAccess(AuthGroup authGroup)
        {
            return GetEffectiveDataPerms(authGroup) != DataPerms.None;
        }

        private bool AuthGroupAppliesToData(AuthGroup authGroup, string dataId)
        {
            var rangeFilter = BuildAuthGroupDataFilter(authGroup);
            if (rangeFilter == null || rangeFilter.IsEmpty)
            {
                return true;
            }

            var filter = new DynamicFilter { Field = Fields.BsonId, Op = FilterOp.Eq, Value = dataId }.And(
                rangeFilter);

            var result = ApiService.Find(FilterResult(new DynamicFindOptions<FormData>
            {
                Filter = filter,
                Select = new DynamicFieldList { DynamicField.Create(Fields.Id, true) },
                Take = 1,
            })).FirstOrDefault();

            return result != null;
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

            return merged.Values.OrderBy(x => x.Id).ToList();
        }

        private static bool? MergeNullablePermission(bool? current, bool? next)
        {
            if (current == true || next == true)
            {
                return true;
            }

            if (current == false || next == false)
            {
                return false;
            }

            return null;
        }

    }
}
