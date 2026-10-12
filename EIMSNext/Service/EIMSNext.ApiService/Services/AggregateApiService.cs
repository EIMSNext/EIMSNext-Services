using EIMSNext.ApiService.RequestModels;
using EIMSNext.Common;
using EIMSNext.Component;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Entities;
using EIMSNext.Persistence.PostgreSql;
using HKH.Mef2.Integration;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using System.Text;
using System.Text.Json;

namespace EIMSNext.ApiService
{
    /// <summary>
    /// 聚合的 API 服务。
    /// </summary>
    /// <remarks>
        /// 由 PostgreSQL 命令执行器在 <c>"FormData"</c> 表上执行（jsonb 字段用 <c>-&gt;&gt;</c> 取值）。
    /// </remarks>
    public class AggregateApiService : ApiServiceBase, IAggregateApiService
    {
        private static readonly HashSet<string> SupportedAggregateFunctions = new(StringComparer.OrdinalIgnoreCase)
        {
            "count", "sum", "avg", "max", "min",
        };

        /// <summary>
        /// 初始化AggregateApiService的新实例。
        /// </summary>
        /// <param name="resolver">服务解析器。</param>
        public AggregateApiService(IResolver resolver) : base(resolver)
        {
        }

        private const int MaxDashboardTake = 1000;

        /// <summary>
        /// 计算聚合结果。
        /// </summary>
        public async Task<List<Dictionary<string, object?>>?> Calucate(DashboardAggregateRequest request)
        {
            var build = BuildDashboardRequest(request, null, false);
            return build == null || build.Authorization.CorpId == null
                ? null
                : await ExecuteAsync(AggregateSqlBuilder.BuildRows(build.Request), build.Authorization.CorpId, build.Request.DataSource.Id);
        }

        /// <summary>
        /// 统计数量。
        /// </summary>
        public async Task<long> Count(DashboardAggregateRequest request)
        {
            var build = BuildDashboardRequest(request, null, false);
            return build == null ? 0 : await ExecuteCountAsync(build);
        }

        /// <summary>
        /// 预览聚合结果。
        /// </summary>
        public async Task<List<Dictionary<string, object?>>?> Preview(DashboardAggregatePreviewRequest request)
        {
            var build = BuildDashboardRequest(request, request.Details, true);
            return build == null || build.Authorization.CorpId == null
                ? null
                : await ExecuteAsync(AggregateSqlBuilder.BuildRows(build.Request), build.Authorization.CorpId, build.Request.DataSource.Id);
        }

        /// <summary>
        /// 预览聚合结果数量。
        /// </summary>
        public async Task<long> PreviewCount(DashboardAggregatePreviewRequest request)
        {
            var build = BuildDashboardRequest(request, request.Details, true);
            return build == null ? 0 : await ExecuteCountAsync(build);
        }

        /// <summary>
        /// 在 "FormData" 表上执行聚合 SQL。
        /// </summary>
        private async Task<List<Dictionary<string, object?>>> ExecuteAsync(
            AggregateSqlBuilder.SqlStatement statement,
            string corpId,
            string? formId = null)
        {
            var rows = new List<Dictionary<string, object?>>();
            await using var command = CreateCommand(statement.Sql, corpId, statement.Parameters);
            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    row[reader.GetName(i)] = value is DBNull ? null : value;
                }

                NormalizeAggregateRow(row, formId);

                rows.Add(row);
            }

            return rows;
        }

        private async Task<long> ExecuteCountAsync(DashboardAggregateBuild build)
        {
            var sql = AggregateSqlBuilder.BuildCount(build.Request, null);
            await using var command = CreateCommand(sql.Sql, build.Authorization.CorpId ?? string.Empty, sql.Parameters);
            var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
            var count = result is null or DBNull ? 0L : Convert.ToInt64(result);
            return build.CountLimit.HasValue ? Math.Min(count, build.CountLimit.Value) : count;
        }

        private void NormalizeAggregateRow(Dictionary<string, object?> row, string? formId)
        {
            if (row.TryGetValue("data", out var rawData))
            {
                var data = rawData switch
                {
                    string json when !string.IsNullOrWhiteSpace(json) => DynamicJsonbReader.Parse(json),
                    JsonDocument document => DynamicJsonbReader.Parse(document.RootElement.GetRawText()),
                    JsonElement element when element.ValueKind == JsonValueKind.Object => DynamicJsonbReader.Parse(element.GetRawText()),
                    _ => null,
                };
                if (data is not null) row["data"] = data;
            }

            NormalizeJsonObject(row, "createBy");
            NormalizeJsonObject(row, "updateBy");

            if (formId is null || !row.ContainsKey("dataTitle") || row["dataTitle"] is not null ||
                row["data"] is not Dictionary<string, object?> dataValues)
            {
                return;
            }

            var formDef = Resolver.Resolve<EIMSNext.Service.Contracts.IFormDefService>().Get(formId);
            if (formDef is null) return;

            var formData = new FormData
            {
                Id = row.TryGetValue("id", out var id) ? id?.ToString() ?? string.Empty : string.Empty,
                AppId = row.TryGetValue("appId", out var appId) ? appId?.ToString() ?? string.Empty : string.Empty,
                FormId = formId,
                Data = dataValues,
            };
            row["dataTitle"] = Resolver.Resolve<DataTitleResolver>().ResolveDataTitle(formData, formDef);
        }

        private static void NormalizeJsonObject(Dictionary<string, object?> row, string name)
        {
            if (!row.TryGetValue(name, out var raw) || raw is not string json || string.IsNullOrWhiteSpace(json)) return;
            try
            {
                row[name] = JsonSerializer.Deserialize<Operator>(json);
            }
            catch (JsonException)
            {
                row[name] = null;
            }
        }

        /// <summary>
        /// 创建绑定到当前 DbContext 连接的 PostgreSQL 命令。
        /// </summary>
        private DbCommand CreateCommand(string sql, string corpId, IReadOnlyList<object?>? parameters = null)
        {
            var dbContext = Resolver.GetRepository<FormData>().DbContext;
            return PostgreSqlCommandBuilder.Create(dbContext, sql, corpId ?? string.Empty, parameters);
        }

        private DashboardAggregateBuild? BuildDashboardRequest(
            DashboardAggregateRequest request, string? previewDetails, bool isPreview)
        {
            if (string.IsNullOrWhiteSpace(request.ItemId)) return null;
            var item = Resolver.GetRepository<DashboardItemDef>().Get(request.ItemId);
            if (item == null || item.DeleteFlag) return null;

            var rawDetails = isPreview ? previewDetails : item.Details;
            if (string.IsNullOrWhiteSpace(rawDetails)) return null;
            try
            {
                using var document = JsonDocument.Parse(rawDetails);
                var root = document.RootElement;
                if (!IsDetailsCompatibleWithItem(root, item.ItemType)) return null;

                var dataSource = ReadDataSource(root);
                if (dataSource == null) return null;
                var authorization = AuthorizeDashboardItem(item, dataSource.Id, isPreview);
                if (!authorization.Allowed) return null;

                var aggregateFilter = MergeFilters(ReadConfiguredFilter(root), request.Filter);
                aggregateFilter = WrapFilter(aggregateFilter, dataSource.Id, authorization.CorpId);
                aggregateFilter = aggregateFilter.And(authorization.DataFilter);

                var aggregateRequest = new AggCalcRequest
                {
                    ItemId = item.Id,
                    DataSource = dataSource,
                    Filter = aggregateFilter,
                    Sort = request.Sort,
                    Skip = Math.Max(request.Skip ?? 0, 0),
                };

                int? countLimit = null;
                if (string.Equals(item.ItemType, "chart", StringComparison.OrdinalIgnoreCase))
                {
                    aggregateRequest.Dimensions = ReadConfiguredDimensions(root, "dimension1")
                        .Concat(ReadConfiguredDimensions(root, "dimension2")).ToList();
                    aggregateRequest.Metrics = ReadConfiguredMetrics(root, "metrics")
                        .Concat(ReadConfiguredProgressTargetMetric(root))
                        .GroupBy(metric => $"{metric.Id}_{metric.AggFun}", StringComparer.OrdinalIgnoreCase)
                        .Select(group => group.First()).ToList();
                    if (aggregateRequest.Metrics.Count == 0 || !HasSupportedAggregateFunctions(aggregateRequest)) return null;
                    aggregateRequest.Take = ReadConfiguredTake(root, "takeEnable", "take") ?? MaxDashboardTake;
                }
                else if (string.Equals(item.ItemType, "detailTable", StringComparison.OrdinalIgnoreCase))
                {
                    aggregateRequest.DisplayFields = GetConfiguredDisplayFields(root).ToList();
                    if (aggregateRequest.DisplayFields.Count == 0) return null;
                    var configuredLimit = ReadConfiguredTake(root, "showTop", "take");
                    countLimit = configuredLimit;
                    var displayFieldSet = aggregateRequest.DisplayFields.ToHashSet(StringComparer.OrdinalIgnoreCase);
                    aggregateRequest.Sort = request.Sort?.Select(sort => new SortItem
                    {
                        Id = displayFieldSet.Contains(sort.Id) && !Fields.IsSystemField(sort.Id) ? $"{Fields.Data}.{sort.Id}" : sort.Id,
                        Type = sort.Type,
                        Dir = sort.Dir,
                    }).ToList();
                    var defaultPageSize = ReadInt(root, "pageSize") ?? 20;
                    var requestedTake = request.Take.GetValueOrDefault(defaultPageSize);
                    aggregateRequest.Take = ClampTake(requestedTake, configuredLimit);
                    if (configuredLimit.HasValue)
                    {
                        if (aggregateRequest.Skip >= configuredLimit.Value) aggregateRequest.Take = 0;
                        else aggregateRequest.Take = Math.Min(aggregateRequest.Take!.Value, configuredLimit.Value - aggregateRequest.Skip.Value);
                    }
                }
                else return null;

                var scope = authorization.FormFieldPermissions;
                if (scope != null)
                {
                    if (!AreRequestedFieldsVisible(aggregateRequest, scope)) return null;
                }
                return new DashboardAggregateBuild(aggregateRequest, authorization, countLimit);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private AggregateAuthorization AuthorizeDashboardItem(DashboardItemDef item, string formId, bool isPreview)
        {
            if (IdentityContext.IdentityType == IdentityType.Public)
            {
                if (isPreview) return AggregateAuthorization.Denied;
                var validator = Resolver.Resolve<IPublicAccessValidator>();
                if (!validator.CanReadDashboardItem(item.Id) || !validator.CanReadDashboardForm(formId))
                    return AggregateAuthorization.Denied;
                return new AggregateAuthorization(true, validator.GetCurrentSetting()?.CorpId ?? string.Empty, null);
            }

            var permissionEvaluator = Resolver.Resolve<TenantAccessEvaluator>();
            if (isPreview)
            {
                permissionEvaluator.EnsureCanManageApp(item.AppId);
            }
            else if (!permissionEvaluator.GetUsageDashboardIdsForCurrentEmployee(item.AppId).Contains(item.DashboardId))
            {
                return AggregateAuthorization.Denied;
            }

            var scope = Resolver.Resolve<FormDataReadScopeResolver>().Resolve(formId);
            return scope.CanRead
                ? new AggregateAuthorization(true, ServiceContext.CorpId, scope.DataFilter, scope.FormFieldPermissions)
                : AggregateAuthorization.Denied;
        }

        private static bool IsDetailsCompatibleWithItem(JsonElement root, string itemType)
        {
            if (!root.TryGetProperty("kind", out var kind) || kind.ValueKind != JsonValueKind.String) return true;
            var detailsKind = kind.GetString();
            return string.Equals(detailsKind, itemType, StringComparison.OrdinalIgnoreCase) ||
                (string.Equals(itemType, "detailTable", StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(detailsKind, "detail-table", StringComparison.OrdinalIgnoreCase));
        }

        private static AgDataSource? ReadDataSource(JsonElement root)
        {
            if (!root.TryGetProperty("datasource", out var source) || source.ValueKind != JsonValueKind.Object ||
                !source.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(id.GetString()))
                return null;
            return new AgDataSource { Id = id.GetString()!, Type = AgDataSourceType.Form };
        }

        private static IEnumerable<Dimension> ReadConfiguredDimensions(JsonElement root, string property)
        {
            if (!root.TryGetProperty(property, out var values) || values.ValueKind != JsonValueKind.Array) yield break;
            foreach (var value in values.EnumerateArray())
            {
                if (!value.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(id.GetString())) continue;
                yield return new Dimension
                {
                    Id = id.GetString()!,
                    Type = value.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String ? type.GetString()! : FieldType.Input,
                };
            }
        }

        private static IEnumerable<Metric> ReadConfiguredMetrics(JsonElement root, string property)
        {
            if (!root.TryGetProperty(property, out var values) || values.ValueKind != JsonValueKind.Array) yield break;
            foreach (var value in values.EnumerateArray())
                if (TryReadMetric(value, out var metric)) yield return metric;
        }

        private static IEnumerable<Metric> ReadConfiguredProgressTargetMetric(JsonElement root)
        {
            if (!root.TryGetProperty("progress", out var progress) || progress.ValueKind != JsonValueKind.Object ||
                !progress.TryGetProperty("targetType", out var targetType) || !string.Equals(targetType.GetString(), "metric", StringComparison.OrdinalIgnoreCase) ||
                !progress.TryGetProperty("targetMetric", out var target) || !TryReadMetric(target, out var metric)) yield break;
            yield return metric;
        }

        private static bool TryReadMetric(JsonElement value, out Metric metric)
        {
            metric = new Metric();
            if (!value.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(id.GetString())) return false;
            metric.Id = id.GetString()!;
            metric.Type = value.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String ? type.GetString()! : FieldType.Input;
            metric.AggFun = value.TryGetProperty("aggFun", out var agg) && agg.ValueKind == JsonValueKind.String ? agg.GetString()! : "count";
            return true;
        }

        private static DynamicFilter? ReadConfiguredFilter(JsonElement root)
        {
            if (!root.TryGetProperty("filter", out var filter) || filter.ValueKind != JsonValueKind.Object) return null;
            return JsonSerializer.Deserialize<ConditionList>(filter.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })?.ToDynamicFilter();
        }

        private static DynamicFilter? MergeFilters(DynamicFilter? fixedFilter, DynamicFilter? runtimeFilter)
        {
            if (fixedFilter == null || fixedFilter.IsEmpty) return runtimeFilter;
            if (runtimeFilter == null || runtimeFilter.IsEmpty) return fixedFilter;
            return new DynamicFilter { Rel = FilterRel.And, Items = [fixedFilter, runtimeFilter] };
        }

        private static int? ReadConfiguredTake(JsonElement root, string enabledProperty, string takeProperty)
        {
            if (!root.TryGetProperty(enabledProperty, out var enabled) || enabled.ValueKind != JsonValueKind.True) return null;
            var take = ReadInt(root, takeProperty);
            return take.HasValue ? Math.Clamp(take.Value, 1, MaxDashboardTake) : null;
        }

        private static int? ReadInt(JsonElement root, string property) =>
            root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result) ? result : null;

        private static int ClampTake(int take, int? configuredLimit) => Math.Clamp(take <= 0 ? 20 : take, 1, Math.Min(configuredLimit ?? MaxDashboardTake, MaxDashboardTake));

        /// <summary>
        /// 计算聚合结果。
        /// </summary>
        public async Task<List<Dictionary<string, object?>>?> Calucate(AggCalcRequest request)
        {
            return await Calucate(request, ServiceContext.CorpId);
        }

        /// <summary>
        /// 计算聚合结果。
        /// </summary>
        public async Task<List<Dictionary<string, object?>>?> Calucate(AggCalcRequest request, string corpId)
        {
            if (request.DataSource?.Type != AgDataSourceType.Form) return null;
            var authorization = Authorize(request, corpId);
            if (!authorization.Allowed) return null;

            var filter = WrapFilter(request.Filter, request.DataSource.Id, authorization.CorpId);
            filter = filter.And(authorization.DataFilter)!;
            request.Filter = filter;
            if (authorization.CorpId == null) return null;

            var sql = AggregateSqlBuilder.BuildRows(request);
            return await ExecuteAsync(sql, authorization.CorpId, request.DataSource.Id);
        }

        /// <summary>
        /// 统计数量。
        /// </summary>
        public async Task<long> Count(AggCalcRequest request)
        {
            return await Count(request, ServiceContext.CorpId);
        }

        /// <summary>
        /// 统计数量。
        /// </summary>
        public async Task<long> Count(AggCalcRequest request, string corpId)
        {
            if (request.DataSource?.Type != AgDataSourceType.Form) return 0;
            var authorization = Authorize(request, corpId);
            if (!authorization.Allowed) return 0;

            var filter = WrapFilter(request.Filter, request.DataSource.Id, authorization.CorpId);
            filter = filter.And(authorization.DataFilter)!;
            request.Filter = filter;

            var sql = AggregateSqlBuilder.BuildCount(request, null);
            await using var command = CreateCommand(sql.Sql, authorization.CorpId ?? string.Empty, sql.Parameters);
            var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
            return result is null or DBNull ? 0 : Convert.ToInt64(result);
        }

        private AggregateAuthorization Authorize(AggCalcRequest request, string corpId)
        {
            if (!HasSupportedAggregateFunctions(request))
            {
                return AggregateAuthorization.Denied;
            }

            if (IdentityContext.IdentityType == IdentityType.Public)
            {
                var validator = Resolver.Resolve<IPublicAccessValidator>();
                if (!validator.CanReadDashboardItem(request.ItemId ?? string.Empty) ||
                    !validator.CanReadDashboardForm(request.DataSource.Id) ||
                    !IsRequestBoundToDashboardItem(request, true))
                {
                    return AggregateAuthorization.Denied;
                }

                return new AggregateAuthorization(true, validator.GetCurrentSetting()?.CorpId ?? string.Empty, null);
            }

            if (!string.IsNullOrWhiteSpace(request.ItemId) && !IsRequestBoundToDashboardItem(request))
            {
                return AggregateAuthorization.Denied;
            }

            var scope = Resolver.Resolve<FormDataReadScopeResolver>().Resolve(request.DataSource.Id);
            if (!scope.CanRead || !AreRequestedFieldsVisible(request, scope.FormFieldPermissions))
            {
                return AggregateAuthorization.Denied;
            }

            return new AggregateAuthorization(true, corpId, scope.DataFilter);
        }

        private bool IsRequestBoundToDashboardItem(AggCalcRequest request, bool validateFields = false)
        {
            if (string.IsNullOrWhiteSpace(request.ItemId))
            {
                return false;
            }

            var item = Resolver.GetRepository<DashboardItemDef>().Get(request.ItemId);
            if (item == null || item.DeleteFlag)
            {
                return false;
            }

            try
            {
                using var details = JsonDocument.Parse(item.Details);
                if (!details.RootElement.TryGetProperty("datasource", out var source) ||
                    !source.TryGetProperty("id", out var formId) ||
                    formId.ValueKind != JsonValueKind.String ||
                    !string.Equals(formId.GetString(), request.DataSource.Id, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (!validateFields) return true;
                if (string.Equals(item.ItemType, "detailTable", StringComparison.OrdinalIgnoreCase))
                {
                    return IsPublicDetailTableRequestValid(request, item, details.RootElement);
                }
                if (!string.Equals(item.ItemType, "chart", StringComparison.OrdinalIgnoreCase)) return false;

                if (!IsConfiguredChartShapeValid(request, details.RootElement)) return false;

                return ContainsConfiguredFilter(request.Filter, details.RootElement) &&
                    AreFilterFieldsConfigured(request.Filter, item, details.RootElement);
            }
            catch
            {
                return false;
            }
        }

        private static bool SetEquals(IEnumerable<string> requested, HashSet<string> configured) =>
            requested.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(configured);

        internal static bool HasSupportedAggregateFunctions(AggCalcRequest request) =>
            (request.Metrics ?? []).All(metric => !string.IsNullOrWhiteSpace(metric.AggFun) && SupportedAggregateFunctions.Contains(metric.AggFun));

        internal static bool IsConfiguredChartShapeValid(AggCalcRequest request, JsonElement root)
        {
            var allowedDimensions = GetConfiguredFields(root, "dimension1")
                .Concat(GetConfiguredFields(root, "dimension2"))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var allowedMetrics = GetConfiguredMetrics(root, "metrics")
                .Concat(GetConfiguredProgressTargetMetric(root))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (allowedMetrics.Count == 0 ||
                !SetEquals((request.Dimensions ?? []).Select(x => x.Id), allowedDimensions) ||
                !SetEquals((request.Metrics ?? []).Select(x => $"{x.Id}_{x.AggFun}"), allowedMetrics)) return false;

            var configuredSorts = GetConfiguredAggregateSorts(root).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return (request.Sort ?? []).All(sort => configuredSorts.Contains($"{sort.Id}:{sort.Dir}")) &&
                (request.DisplayFields?.Count ?? 0) == 0;
        }

        private bool IsPublicDetailTableRequestValid(AggCalcRequest request, DashboardItemDef item, JsonElement root)
        {
            if ((request.Dimensions?.Count ?? 0) > 0 || (request.Metrics?.Count ?? 0) > 0) return false;
            var configuredFields = GetConfiguredDisplayFields(root).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!SetEquals(request.DisplayFields ?? [], configuredFields)) return false;
            var configuredSortFields = GetConfiguredSortFields(root).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if ((request.Sort ?? []).Any(sort => !configuredSortFields.Contains(sort.Id))) return false;
            return ContainsConfiguredFilter(request.Filter, root) && AreFilterFieldsConfigured(request.Filter, item, root);
        }

        private static IEnumerable<string> GetConfiguredDisplayFields(JsonElement root)
        {
            if (!root.TryGetProperty("displayFields", out var fields) || fields.ValueKind != JsonValueKind.Array) yield break;
            foreach (var field in fields.EnumerateArray())
            {
                if (field.TryGetProperty("field", out var id) && id.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(id.GetString()))
                    yield return id.GetString()!;
            }
        }

        private static IEnumerable<string> GetConfiguredSortFields(JsonElement root)
        {
            if (!root.TryGetProperty("sort", out var sort) || sort.ValueKind != JsonValueKind.Object ||
                !sort.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) yield break;
            foreach (var item in items.EnumerateArray())
            {
                if (!item.TryGetProperty("field", out var field) || field.ValueKind != JsonValueKind.Object ||
                    !field.TryGetProperty("field", out var id) || id.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(id.GetString())) continue;
                yield return id.GetString()!;
            }
        }

        internal static bool ContainsConfiguredFilter(DynamicFilter? requestFilter, JsonElement root)
        {
            if (!root.TryGetProperty("filter", out var configuredElement) || configuredElement.ValueKind != JsonValueKind.Object) return true;
            var configured = JsonSerializer.Deserialize<ConditionList>(configuredElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })?.ToDynamicFilter();
            if (configured == null || configured.IsEmpty) return true;
            if (requestFilter == null) return false;
            if (FiltersEqual(requestFilter, configured)) return true;
            return string.Equals(requestFilter.Rel, FilterRel.And, StringComparison.OrdinalIgnoreCase) &&
                (requestFilter.Items ?? []).Any(item => FiltersEqual(item, configured));
        }

        private static bool FiltersEqual(DynamicFilter left, DynamicFilter right)
        {
            if (!string.Equals(left.Field, right.Field, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(left.Type, right.Type, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(left.Op, right.Op, StringComparison.OrdinalIgnoreCase) ||
                left.ValueIsExp != right.ValueIsExp || left.ValueIsField != right.ValueIsField ||
                !JsonElement.DeepEquals(JsonSerializer.SerializeToElement(left.Value), JsonSerializer.SerializeToElement(right.Value)))
            {
                return false;
            }

            var leftItems = left.Items ?? [];
            var rightItems = right.Items ?? [];
            if (leftItems.Count != rightItems.Count) return false;
            if (leftItems.Count == 0) return true;
            if (!string.Equals(left.Rel, right.Rel, StringComparison.OrdinalIgnoreCase)) return false;
            var unmatched = new List<DynamicFilter>(leftItems);
            foreach (var rightItem in rightItems)
            {
                var matchIndex = unmatched.FindIndex(leftItem => FiltersEqual(leftItem, rightItem));
                if (matchIndex < 0) return false;
                unmatched.RemoveAt(matchIndex);
            }
            return true;
        }

        private static IEnumerable<string> GetConfiguredAggregateSorts(JsonElement root)
        {
            if (!root.TryGetProperty("sort", out var sort) || sort.ValueKind != JsonValueKind.Object ||
                !sort.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) yield break;
            foreach (var item in items.EnumerateArray())
            {
                if (!item.TryGetProperty("field", out var field) || field.ValueKind != JsonValueKind.Object ||
                    !field.TryGetProperty("field", out var id) || id.ValueKind != JsonValueKind.String ||
                    !item.TryGetProperty("sort", out var direction) || direction.ValueKind != JsonValueKind.Number || direction.GetInt32() == 0) continue;
                var configuredId = id.GetString()!;
                var metric = GetConfiguredMetrics(root, "metrics")
                    .FirstOrDefault(x => string.Equals(x[..x.LastIndexOf('_')], configuredId, StringComparison.OrdinalIgnoreCase));
                yield return $"{metric ?? configuredId}:{direction.GetInt32()}";
            }
        }

        private static IEnumerable<string> GetConfiguredFields(JsonElement root, string property, string? nested = null)
        {
            if (!root.TryGetProperty(property, out var element)) yield break;
            if (nested != null)
            {
                if (!element.TryGetProperty(nested, out element)) yield break;
            }
            if (element.ValueKind != JsonValueKind.Array) yield break;
            foreach (var field in element.EnumerateArray())
            {
                if (field.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(id.GetString()))
                    yield return id.GetString()!;
            }
        }

        private static IEnumerable<string> GetConfiguredMetrics(JsonElement root, string property, string? nested = null)
        {
            if (!root.TryGetProperty(property, out var element)) yield break;
            if (nested != null)
            {
                if (!element.TryGetProperty(nested, out element)) yield break;
                if (element.ValueKind != JsonValueKind.Object) yield break;
                if (element.TryGetProperty("id", out var targetId) && targetId.ValueKind == JsonValueKind.String)
                {
                    var agg = element.TryGetProperty("aggFun", out var targetAgg) && targetAgg.ValueKind == JsonValueKind.String ? targetAgg.GetString() : "count";
                    yield return $"{targetId.GetString()}_{agg}";
                }
                yield break;
            }
            if (element.ValueKind != JsonValueKind.Array) yield break;
            foreach (var field in element.EnumerateArray())
            {
                if (field.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(id.GetString()))
                {
                    var agg = field.TryGetProperty("aggFun", out var aggElement) && aggElement.ValueKind == JsonValueKind.String ? aggElement.GetString() : "count";
                    yield return $"{id.GetString()}_{agg}";
                }
            }
        }

        private static IEnumerable<string> GetConfiguredProgressTargetMetric(JsonElement root)
        {
            if (!root.TryGetProperty("progress", out var progress) || progress.ValueKind != JsonValueKind.Object ||
                !progress.TryGetProperty("targetType", out var targetType) || targetType.ValueKind != JsonValueKind.String ||
                !string.Equals(targetType.GetString(), "metric", StringComparison.OrdinalIgnoreCase))
            {
                yield break;
            }

            foreach (var metric in GetConfiguredMetrics(root, "progress", "targetMetric")) yield return metric;
        }

        private bool AreFilterFieldsConfigured(DynamicFilter? filter, DashboardItemDef chartItem, JsonElement root)
        {
            if (filter == null) return true;
            var allowed = GetConfiguredFields(root, "dimension1")
                .Concat(GetConfiguredFields(root, "dimension2"))
                .Concat(GetConfiguredMetrics(root, "metrics").Select(x => x[..x.LastIndexOf('_')]))
                .Concat(GetConfiguredDisplayFields(root))
                .Concat(GetConfiguredFilterFields(root))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var chartDataSourceId = root.GetProperty("datasource").GetProperty("id").GetString();

            var filterItems = Resolver.GetRepository<DashboardItemDef>().Queryable
                .Where(x => x.DashboardId == chartItem.DashboardId && x.CorpId == chartItem.CorpId && !x.DeleteFlag && x.ItemType == "filter")
                .ToList();
            foreach (var filterItem in filterItems)
            {
                try
                {
                    using var filterDetails = JsonDocument.Parse(filterItem.Details);
                    if (!filterDetails.RootElement.TryGetProperty("targetChartIds", out var targetChartIds) ||
                        targetChartIds.ValueKind != JsonValueKind.Array ||
                        !targetChartIds.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.String && string.Equals(x.GetString(), chartItem.Id, StringComparison.OrdinalIgnoreCase)) ||
                        !filterDetails.RootElement.TryGetProperty("bindings", out var bindings) ||
                        bindings.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var binding in bindings.EnumerateArray())
                    {
                        if (!binding.TryGetProperty("dataSourceId", out var bindingDataSourceId) ||
                            bindingDataSourceId.ValueKind != JsonValueKind.String ||
                            !string.Equals(bindingDataSourceId.GetString(), chartDataSourceId, StringComparison.OrdinalIgnoreCase) ||
                            !binding.TryGetProperty("field", out var field) ||
                            !field.TryGetProperty("field", out var fieldId) || fieldId.ValueKind != JsonValueKind.String)
                        {
                            continue;
                        }
                        allowed.Add(fieldId.GetString()!);
                    }
                }
                catch (JsonException)
                {
                    // Ignore invalid filter items; they cannot grant access to fields.
                }
            }
            return EnumerateFilterFields(filter).All(field =>
            {
                var normalized = NormalizeAggregateField(field);
                return IsSystemAggregateFilterField(normalized) || allowed.Contains(normalized);
            });
        }

        private static string NormalizeAggregateField(string field) =>
            field.StartsWith("data.", StringComparison.OrdinalIgnoreCase) ? field[5..] : field;

        private static bool IsSystemAggregateFilterField(string field) =>
            string.Equals(field, Fields.CorpId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(field, Fields.FormId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(field, Fields.DeleteFlag, StringComparison.OrdinalIgnoreCase) ||
            field.StartsWith("__", StringComparison.Ordinal);

        private static IEnumerable<string> GetConfiguredFilterFields(JsonElement root)
        {
            if (!root.TryGetProperty("filter", out var filter) || filter.ValueKind != JsonValueKind.Object) yield break;
            foreach (var field in EnumerateJsonFilterFields(filter)) yield return field;
        }

        private static IEnumerable<string> EnumerateJsonFilterFields(JsonElement filter)
        {
            if (filter.TryGetProperty("field", out var field) && field.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(field.GetString()))
                yield return field.GetString()!;
            if (!filter.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) yield break;
            foreach (var item in items.EnumerateArray())
                foreach (var nestedField in EnumerateJsonFilterFields(item))
                    yield return nestedField;
        }

        private static bool AreRequestedFieldsVisible(AggCalcRequest request, IReadOnlyCollection<FormFieldPermission>? fieldPerms)
        {
            if (fieldPerms == null)
            {
                return true;
            }

            var requestedFields = (request.Dimensions ?? []).Select(x => x.Id)
                .Concat((request.Metrics ?? []).Select(x => x.Id))
                .Concat(request.DisplayFields ?? [])
                .Concat((request.Sort ?? []).Select(x => x.Id))
                .Concat(EnumerateFilterFields(request.Filter));

            return requestedFields.All(field => IsFieldVisible(field, fieldPerms, request));
        }

        private static IEnumerable<string> EnumerateFilterFields(DynamicFilter? filter)
        {
            if (filter == null)
            {
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(filter.Field))
            {
                yield return filter.Field;
            }

            if (filter.ValueIsField && filter.Value is string valueField)
            {
                yield return valueField;
            }

            foreach (var item in filter.Items ?? [])
            {
                foreach (var field in EnumerateFilterFields(item))
                {
                    yield return field;
                }
            }
        }

        private static bool IsFieldVisible(string? field, IReadOnlyCollection<FormFieldPermission> fieldPerms, AggCalcRequest request)
        {
            if (string.IsNullOrWhiteSpace(field))
            {
                return true;
            }

            var normalized = field.StartsWith("data.", StringComparison.OrdinalIgnoreCase) ? field[5..] : field;
            var root = normalized.Split('.', 2)[0];
            if (Fields.IsSystemField(root))
            {
                return true;
            }

            if (fieldPerms.Any(x => x.Visible && string.Equals(x.Id, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return (request.Metrics ?? []).Any(metric =>
                string.Equals($"{metric.Id}_{metric.AggFun}", normalized, StringComparison.OrdinalIgnoreCase) &&
                fieldPerms.Any(permission => permission.Visible && string.Equals(permission.Id, metric.Id, StringComparison.OrdinalIgnoreCase)));
        }

        private DynamicFilter WrapFilter(DynamicFilter? filter, string formId, string corpId)
        {
            filter ??= new DynamicFilter();
            var scopeFilter = new DynamicFilter
            {
                Rel = FilterRel.And,
                Items =
                [
                    new DynamicFilter { Field = Fields.CorpId, Op = FilterOp.Eq, Value = corpId },
                    new DynamicFilter { Field = Fields.FormId, Op = FilterOp.Eq, Value = formId },
                    new DynamicFilter { Field = Fields.DeleteFlag, Op = FilterOp.Ne, Value = true },
                ],
            };

            if (filter.IsGroup && filter.Rel == FilterRel.And)
            {
                filter.Items!.Insert(0, scopeFilter);
                return filter;
            }

            return new DynamicFilter
            {
                Rel = FilterRel.And,
                Items = [scopeFilter, filter],
            };
        }

        private sealed record DashboardAggregateBuild(AggCalcRequest Request, AggregateAuthorization Authorization, int? CountLimit);

        private sealed record AggregateAuthorization(bool Allowed, string CorpId, DynamicFilter? DataFilter, IReadOnlyCollection<FormFieldPermission>? FormFieldPermissions = null)
        {
            public static AggregateAuthorization Denied { get; } = new(false, string.Empty, null);
        }
    }


    /// <summary>
    /// 把聚合请求编译为 PostgreSQL 语句。
    /// </summary>
    /// <remarks>
    /// <c>$match</c> → <c>where</c>、<c>$group</c> → <c>group by</c>、
    /// <c>$project</c> → <c>select</c> 列表、<c>$sort</c> → <c>order by</c>、
    /// <c>$skip/$limit</c> → <c>offset/fetch</c>。
    /// 表单业务字段存放在 jsonb 列 <c>"Data"</c>，用 <c>-&gt;&gt;</c> 取文本。
    /// </remarks>
    internal static class AggregateSqlBuilder
    {
        /// <summary>
        /// 聚合 SQL 与其位置参数。
        /// </summary>
        /// <param name="Sql">SQL 文本，企业条件使用 <c>@corpId</c> 占位。</param>
        /// <param name="Parameters">按 <c>@p0</c>、<c>@p1</c>… 顺序绑定的位置参数。</param>
        internal readonly record struct SqlStatement(string Sql, IReadOnlyList<object?> Parameters);

        /// <summary>
        /// 生成明细行查询（含聚合度量时为 group by 结果集，否则为投影明细）。
        /// </summary>
        public static SqlStatement BuildRows(AggCalcRequest request)
        {
            var parameters = new List<object?>();
            var where = BuildWhere(request.Filter, parameters);

            var metricSql = BuildMetricSelects(request.Metrics, parameters);
            var dimensionSql = BuildDimensionSelects(request.Dimensions);

            var sql = new StringBuilder();
            if (metricSql.Count > 0 && request.Metrics is { Count: > 0 })
            {
                var groupKeys = (request.Dimensions ?? [])
                    .Where(d => !string.IsNullOrEmpty(d.Id))
                    .Select(d => FieldExpression(d.Id))
                    .ToList();
                if (groupKeys.Count > 0)
                {
                    // 有维度：按维度分组输出维度列 + 度量列。
                    sql.Append("select ").Append(string.Join(", ", dimensionSql));
                    foreach (var metric in metricSql)
                    {
                        sql.Append(", ").Append(metric);
                    }

                    sql.Append(" from \"FormData\" where ").Append(where);
                    sql.Append(" group by ").Append(string.Join(", ", groupKeys));
                }
                else
                {
                    // 无维度：整表聚合成一行。
                    sql.Append("select ").Append(string.Join(", ", metricSql));
                    sql.Append(" from \"FormData\" where ").Append(where);
                }
            }
            else
            {
                // 明细表：投影基础字段与被指定的展示字段。
                sql.Append("select ");
                // 返回原有 FormData 契约：动态字段保留在 data 对象中，
                // 避免把字段名改成 data_xxx 后破坏前端和公开查询调用方。
                sql.Append("\"Id\" as \"id\", \"AppId\" as \"appId\", \"FormId\" as \"formId\", ");
                sql.Append("null::text as \"dataTitle\", \"Data\" as \"data\", ");
                sql.Append("\"CreateBy\" as \"createBy\", \"CreateTime\" as \"createTime\", ");
                sql.Append("\"UpdateBy\" as \"updateBy\", \"UpdateTime\" as \"updateTime\", ");
                sql.Append("\"FlowStatus\" as \"flowStatus\"");

                sql.Append(" from \"FormData\" where ").Append(where);
            }

            AppendOrderBy(sql, request.Sort, parameters, request);
            AppendPaging(sql, request.Skip, request.Take);
            return new SqlStatement(sql.ToString(), parameters);
        }

        /// <summary>
        /// 生成计数语句。
        /// </summary>
        public static SqlStatement BuildCount(AggCalcRequest request, DynamicFilter? extraFilter)
        {
            var parameters = new List<object?>();
            var filter = request.Filter.And(extraFilter);
            var where = BuildWhere(filter, parameters);
            return new SqlStatement($"select count(*) from \"FormData\" where {where}", parameters);
        }

        /// <summary>
        /// 生成 where 子句。企业隔离与软删除条件始终注入。
        /// </summary>
        /// <returns>where 子句正文（不含关键字）。</returns>
        private static string BuildWhere(DynamicFilter? filter, List<object?> parameters)
        {
            filter = DynamicFilterRules.Normalize(filter);
            DynamicFilterValidator.Validate(filter);
            var clauses = new List<string>
            {
                // corpId 由调用方通过 @corpId 绑定，作为企业维度的显式过滤条件。
                "\"CorpId\" = @corpId",
                "not \"DeleteFlag\"",
            };

            var business = BuildFilterBody(filter, parameters);
            if (!string.IsNullOrEmpty(business))
            {
                clauses.Add(business);
            }

            return string.Join(" and ", clauses);
        }

        /// <summary>
        /// 递归把动态过滤条件编译为 SQL 谓词。
        /// </summary>
        /// <returns>SQL 谓词；无法编译时退化为 true。</returns>
        private static string BuildFilterBody(DynamicFilter? filter, List<object?> parameters)
        {
            if (filter == null || filter.IsEmpty && !filter.IsGroup)
            {
                return string.Empty;
            }

            if (filter.IsGroup)
            {
                var children = (filter.Items ?? [])
                    .Select(item => BuildFilterBody(item, parameters))
                    .Where(x => !string.IsNullOrEmpty(x))
                    .ToList();
                if (children.Count == 0)
                {
                    return string.Empty;
                }

                var joined = string.Join(filter.Rel switch
                {
                    FilterRel.Or => " or ",
                    _ => " and ",
                }, children);
                return string.Equals(filter.Rel, FilterRel.Not, StringComparison.OrdinalIgnoreCase)
                    ? $"not ({joined})"
                    : $"({joined})";
            }

            if (string.IsNullOrWhiteSpace(filter.Field))
            {
                return string.Empty;
            }

            var column = FieldExpression(filter.Field);
            var op = filter.Op!;
            var values = NormalizeValues(filter.Value);

            string Add(object? value)
            {
                parameters.Add(value);
                return $"@p{parameters.Count - 1}";
            }

            if (filter.Value is null)
            {
                return op switch
                {
                    FilterOp.Eq => $"{column} is null",
                    FilterOp.Ne or FilterOp.Nin => $"{column} is not null",
                    FilterOp.Empty => $"{column} is null",
                    FilterOp.NotEmpty or FilterOp.Exists => $"{column} is not null",
                    _ => throw new BadRequestException($"运算符 {op} 不接受空过滤值"),
                };
            }

            switch (op)
            {
                case FilterOp.Eq:
                    if (values.Count == 1 && IsDynamicField(filter.Field))
                    {
                        var jsonPath = $"{BuildJsonPath(filter.Field)} ? (@ == {JsonSerializer.Serialize(values[0])})";
                        return $"\"Data\" @? {Add(jsonPath)}::jsonpath";
                    }
                    return values.Count == 1
                        ? $"{column} = {Add(ToSqlValue(filter.Field, values[0]))}"
                        : $"{column} = any({Add(ToSqlArray(filter.Field, values))})";
                case FilterOp.Ne:
                    return values.Count == 1
                        ? $"({column} is null or {column} <> {Add(ToSqlValue(filter.Field, values[0]))})"
                        : $"({column} is null or {column} <> all({Add(ToSqlArray(filter.Field, values))}))";
                case FilterOp.Gt:
                case FilterOp.Gte:
                case FilterOp.Lt:
                case FilterOp.Lte:
                {
                    if (values.Count == 0) return "false";
                    var symbol = op switch
                    {
                        FilterOp.Gt => ">",
                        FilterOp.Gte => ">=",
                        FilterOp.Lt => "<",
                        _ => "<=",
                    };
                    return $"{NumericExpression(filter.Field)} {symbol} {Add(values[0])}::numeric";
                }
                case FilterOp.Between:
                {
                    if (values.Count < 2) return "false";
                    return $"{NumericExpression(filter.Field)} between {Add(values[0])}::numeric and {Add(values[1])}::numeric";
                }
                case FilterOp.In:
                    if (IsDynamicField(filter.Field))
                        return BuildJsonPathMatch(filter.Field, values, Add, negate: false);
                    return values.Count == 0
                        ? "false"
                        : $"{column} = any({Add(ToSqlArray(filter.Field, values))})";
                case FilterOp.AllIn:
                    return values.Count == 0
                        ? "false"
                        : string.Join(" and ", values.Select(value => $"exists (select 1 from jsonb_array_elements_text({JsonArrayExpression(filter.Field)}) as elem where elem = {Add(ToSqlText(value))})"));
                case FilterOp.Nin:
                    if (IsDynamicField(filter.Field))
                        return BuildJsonPathMatch(filter.Field, values, Add, negate: true);
                    return values.Count == 0
                        ? "true"
                        : $"({column} is null or {column} <> all({Add(ToSqlArray(filter.Field, values))}))";
                case FilterOp.Empty:
                    return IsIntegerSystemField(filter.Field)
                        ? $"{column} is null"
                        : $"({column} is null or {column} = '')";
                case FilterOp.NotEmpty:
                    return IsIntegerSystemField(filter.Field)
                        ? $"{column} is not null"
                        : $"({column} is not null and {column} <> '')";
                case FilterOp.Exists:
                    return $"{column} is not null";
                case FilterOp.Text:
                    return values.Count == 0 ? "false" : $"{column} ilike {Add($"%{values[0]}%")}";
                default:
                    throw new BadRequestException($"不支持的过滤运算符: {op}");
            }
        }

        private static List<string> BuildMetricSelects(List<Metric>? metrics, List<object?> parameters)
        {
            var selects = new List<string>();
            foreach (var metric in metrics ?? [])
            {
                if (string.IsNullOrEmpty(metric.Id) || string.IsNullOrEmpty(metric.AggFun)) continue;
                var alias = $"{SanitizeAlias(metric.Id)}_{metric.AggFun.ToLowerInvariant()}";
                var expression = metric.AggFun.ToLowerInvariant() switch
                {
                    "count" => "count(*)",
                    "sum" => $"sum({NumericExpression(metric.Id)})",
                    "avg" => $"avg({NumericExpression(metric.Id)})",
                    "max" => $"max({NumericExpression(metric.Id)})",
                    "min" => $"min({NumericExpression(metric.Id)})",
                    _ => null,
                };
                if (expression == null) continue;
                selects.Add($"{expression} as {QuoteIdentifier(alias)}");
            }

            _ = parameters;
            return selects;
        }

        private static List<string> BuildDimensionSelects(List<Dimension>? dimensions)
        {
            return (dimensions ?? [])
                .Where(d => !string.IsNullOrEmpty(d.Id))
                .Select(d => $"{FieldExpression(d.Id)} as {QuoteIdentifier(SanitizeAlias(d.Id))}")
                .ToList();
        }

        private static void AppendOrderBy(StringBuilder sql, List<SortItem>? sort, List<object?> parameters, AggCalcRequest request)
        {
            if (sort is not { Count: > 0 })
            {
                return;
            }

            var parts = new List<string>();
            foreach (var rule in sort)
            {
                if (string.IsNullOrEmpty(rule.Id)) continue;
                var direction = rule.Dir < 0 ? "desc" : "asc";
                var metricAlias = (request.Metrics ?? [])
                    .Where(metric => !string.IsNullOrWhiteSpace(metric.Id) && !string.IsNullOrWhiteSpace(metric.AggFun))
                    .Select(metric => $"{SanitizeAlias(metric.Id)}_{metric.AggFun.ToLowerInvariant()}")
                    .FirstOrDefault(alias => string.Equals(alias, rule.Id, StringComparison.OrdinalIgnoreCase));
                var dimensionAlias = (request.Dimensions ?? [])
                    .Where(dimension => !string.IsNullOrWhiteSpace(dimension.Id))
                    .Select(dimension => SanitizeAlias(dimension.Id))
                    .FirstOrDefault(alias => string.Equals(alias, SanitizeAlias(rule.Id), StringComparison.OrdinalIgnoreCase));
                var orderExpression = metricAlias is not null
                    ? QuoteIdentifier(metricAlias)
                    : dimensionAlias is not null
                        ? QuoteIdentifier(dimensionAlias)
                        : FieldExpression(rule.Id);
                parts.Add($"{orderExpression} {direction} nulls last");
            }

            if (parts.Count > 0)
            {
                sql.Append(" order by ").Append(string.Join(", ", parts));
            }

            _ = parameters;
        }

        private static void AppendPaging(StringBuilder sql, int? skip, int? take)
        {
            if (take is > 0)
            {
                if (skip is > 0)
                {
                    sql.Append(" offset ").Append(skip.Value);
                }

                sql.Append(" limit ").Append(take.Value);
            }
        }

        /// <summary>
        /// 把业务字段路径编译为 SQL 表达式。
        /// 系统字段直取同名列；业务字段走 jsonb <c>"Data"</c>。
        /// </summary>
        /// <param name="field">字段路径，形如 <c>createTime</c> 或 <c>data.name</c>。</param>
        private static string FieldExpression(string field)
        {
            var normalized = NormalizePath(field);
            if (TryGetSystemColumn(normalized, out var systemColumn))
            {
                return $"\"{systemColumn}\"";
            }

            foreach (var systemRoot in new[] { Fields.CreateBy, Fields.UpdateBy })
            {
                var prefix = $"{systemRoot}.";
                if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    var nested = normalized[prefix.Length..].Split('.', StringSplitOptions.RemoveEmptyEntries);
                    var systemExpression = $"\"{(systemRoot.Equals(Fields.CreateBy, StringComparison.OrdinalIgnoreCase) ? "CreateBy" : "UpdateBy")}\"";
                    foreach (var segment in nested)
                        systemExpression += $" ->> '{EscapeLiteral(segment)}'";
                    return systemExpression;
                }
            }

            if (string.Equals(normalized, Fields.DataTitle, StringComparison.OrdinalIgnoreCase))
                return "null::text";

            if (normalized.Contains('>'))
            {
                var path = BuildJsonPath(normalized);
                return $"jsonb_path_query_first(\"Data\", '{EscapeLiteral(path)}') #>> '{{}}'";
            }

            var segments = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries);
            var expression = "\"Data\"";
            for (var i = 0; i < segments.Length; i++)
            {
                var isLast = i == segments.Length - 1;
                expression += isLast
                    ? $" ->> '{EscapeLiteral(segments[i])}'"
                    : $" -> '{EscapeLiteral(segments[i])}'";
            }

            return expression;
        }

        private static string JsonColumn(string field)
        {
            var segments = NormalizePath(field).Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (NormalizePath(field).Contains('>'))
            {
                return $"jsonb_path_query_array(\"Data\", '{EscapeLiteral(BuildJsonPath(NormalizePath(field)))}')";
            }

            var expression = "\"Data\"";
            foreach (var segment in segments)
            {
                expression += $" -> '{EscapeLiteral(segment)}'";
            }

            return expression;
        }

        private static string NumericExpression(string field)
        {
            var expression = FieldExpression(field);
            return $"case when {expression} ~ '^-?[0-9]+([.][0-9]+)?$' then {expression}::numeric end";
        }

        private static string JsonArrayExpression(string field)
        {
            var json = JsonColumn(field);
            return $"case when jsonb_typeof({json}) = 'array' then {json} else '[]'::jsonb end";
        }

        private static string BuildJsonPathMatch(
            string field,
            IReadOnlyCollection<object?> values,
            Func<object?, string> add,
            bool negate)
        {
            if (values.Count == 0) return negate ? "true" : "false";
            var predicate = string.Join(" || ", values.Select(value => $"@ == {JsonSerializer.Serialize(value)}"));
            var exists = $"\"Data\" @? {add($"{BuildJsonPath(field)} ? ({predicate})")}::jsonpath";
            return negate ? $"not ({exists})" : exists;
        }

        private static string NormalizePath(string field)
        {
            var normalized = field.Trim();
            return normalized.StartsWith($"{Fields.Data}.", StringComparison.OrdinalIgnoreCase)
                ? normalized[$"{Fields.Data}.".Length..]
                : normalized;
        }

        private static bool TryGetSystemColumn(string field, out string column)
        {
            column = field.ToLowerInvariant() switch
            {
                "id" => "Id",
                "appid" => "AppId",
                "formid" => "FormId",
                "corpid" => "CorpId",
                "createby" => "CreateBy",
                "createtime" => "CreateTime",
                "updateby" => "UpdateBy",
                "updatetime" => "UpdateTime",
                "deleteflag" => "DeleteFlag",
                "flowstatus" => "FlowStatus",
                _ => string.Empty,
            };
            return column.Length > 0;
        }

        private static bool IsIntegerSystemField(string? field)
            => string.Equals(field, Fields.FlowStatus, StringComparison.OrdinalIgnoreCase);

        private static bool IsDynamicField(string field)
        {
            var normalized = NormalizePath(field);
            return !TryGetSystemColumn(normalized, out _) &&
                !normalized.StartsWith($"{Fields.CreateBy}.", StringComparison.OrdinalIgnoreCase) &&
                !normalized.StartsWith($"{Fields.UpdateBy}.", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(normalized, Fields.DataTitle, StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildJsonPath(string field)
        {
            var builder = new StringBuilder("$");
            var levels = NormalizePath(field).Split('>', StringSplitOptions.RemoveEmptyEntries);
            for (var levelIndex = 0; levelIndex < levels.Length; levelIndex++)
            {
                if (levelIndex > 0) builder.Append("[*]");
                foreach (var segment in levels[levelIndex].Split('.', StringSplitOptions.RemoveEmptyEntries))
                {
                    builder.Append(".\"")
                        .Append(segment.Replace("\\", "\\\\").Replace("\"", "\\\""))
                        .Append('"');
                }
            }
            return builder.ToString();
        }

        private static List<object?> NormalizeValues(object? value)
        {
            return value switch
            {
                null => [],
                string text => [text],
                System.Text.Json.JsonElement json => JsonElementToValues(json),
                System.Collections.IEnumerable sequence => sequence.Cast<object?>().ToList(),
                _ => [value],
            };
        }

        private static List<object?> JsonElementToValues(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                return element.EnumerateArray().Select(item => (object?)item.ToString()).ToList();
            }

            return [element.ToString()];
        }

        private static object? ToSqlText(object? value)
        {
            return value switch
            {
                null => null,
                bool flag => flag ? "true" : "false",
                DateTime dateTime => dateTime.ToUniversalTime().ToString("O"),
                _ => value.ToString(),
            };
        }

        private static object? ToSqlValue(string? field, object? value)
            => IsIntegerSystemField(field) ? ToInt32(value) : ToSqlText(value);

        private static object ToSqlArray(string? field, IReadOnlyCollection<object?> values)
            => IsIntegerSystemField(field)
                ? values.Select(ToInt32).ToArray()
                : values.Select(value => ToSqlText(value)?.ToString() ?? string.Empty).ToArray();

        private static int ToInt32(object? value)
        {
            if (value is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number)) return number;
                value = element.ToString();
            }

            if (value is string text && int.TryParse(text, out var parsed)) return parsed;
            return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string SanitizeAlias(string field)
        {
            var normalized = NormalizePath(field);
            return normalized.Replace(".", "_").Replace(">", "_").Replace(" ", "_");
        }

        private static string QuoteIdentifier(string identifier) => $"\"{identifier.Replace("\"", "\"\"")}\"";

        private static string EscapeLiteral(string value) => value.Replace("'", "''");
    }
}
