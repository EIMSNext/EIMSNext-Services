using EIMSNext.ApiService.RequestModels;
using EIMSNext.Common;
using EIMSNext.Component;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Entities;
using HKH.Mef2.Integration;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using System.Text.Json;

namespace EIMSNext.ApiService
{
    public class AggregateApiService : ApiServiceBase, IAggregateApiService
    {
        private static readonly HashSet<string> SupportedAggregateFunctions = new(StringComparer.OrdinalIgnoreCase)
        {
            "count", "sum", "avg", "max", "min",
        };

        public AggregateApiService(IResolver resolver) : base(resolver)
        {
            AggregateService = resolver.Resolve<AggregateService>();
        }

        private AggregateService AggregateService { get; set; }

        private const int MaxDashboardTake = 1000;

        public async Task<IAsyncCursor<BsonDocument>?> Calucate(DashboardAggregateRequest request)
        {
            var build = BuildDashboardRequest(request, null, false);
            return build == null ? null : await Execute(build);
        }

        public async Task<long> Count(DashboardAggregateRequest request)
        {
            var build = BuildDashboardRequest(request, null, false);
            return build == null ? 0 : await ExecuteCount(build);
        }

        public async Task<IAsyncCursor<BsonDocument>?> Preview(DashboardAggregatePreviewRequest request)
        {
            var build = BuildDashboardRequest(request, request.Details, true);
            return build == null ? null : await Execute(build);
        }

        public async Task<long> PreviewCount(DashboardAggregatePreviewRequest request)
        {
            var build = BuildDashboardRequest(request, request.Details, true);
            return build == null ? 0 : await ExecuteCount(build);
        }

        private async Task<IAsyncCursor<BsonDocument>?> Execute(DashboardAggregateBuild build)
        {
            var collection = AggregateService.GetCollection("FormData");
            var filter = WrapFilter(build.Request.Filter, build.Request.DataSource.Id, build.Authorization.CorpId)
                .And(build.Authorization.DataFilter)!;
            build.Request.Filter = filter;
            var pipeline = PipelineDefinition<BsonDocument, BsonDocument>.Create(
                PipelineBuilder.BuildPipeline(collection, build.Request, ServiceContext));
            return await collection.AggregateAsync(pipeline);
        }

        private async Task<long> ExecuteCount(DashboardAggregateBuild build)
        {
            var collection = AggregateService.GetCollection("FormData");
            var filter = WrapFilter(build.Request.Filter, build.Request.DataSource.Id, build.Authorization.CorpId)
                .And(build.Authorization.DataFilter)!;
            var count = await collection.CountDocumentsAsync(filter.ToFilterDefinition<BsonDocument>());
            return build.CountLimit.HasValue ? Math.Min(count, build.CountLimit.Value) : count;
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

                var aggregateRequest = new AggCalcRequest
                {
                    ItemId = item.Id,
                    DataSource = dataSource,
                    Filter = MergeFilters(ReadConfiguredFilter(root), request.Filter),
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

                // Only the component definition is subject to field visibility. Runtime filters and sorts
                // intentionally remain dynamic; data permission and Mongo operator value protections still apply.
                var scope = authorization.FormFieldPermissions;
                if (scope != null)
                {
                    var configuredFields = new AggCalcRequest
                    {
                        DataSource = aggregateRequest.DataSource,
                        Dimensions = aggregateRequest.Dimensions,
                        Metrics = aggregateRequest.Metrics,
                        DisplayFields = aggregateRequest.DisplayFields,
                    };
                    if (!AreRequestedFieldsVisible(configuredFields, scope)) return null;
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

        public async Task<IAsyncCursor<BsonDocument>?> Calucate(AggCalcRequest request)
        {
            return await Calucate(request, ServiceContext.CorpId);
        }

        public async Task<IAsyncCursor<BsonDocument>?> Calucate(AggCalcRequest request, string corpId)
        {
            if (request.DataSource?.Type != AgDataSourceType.Form) return null;
            var authorization = Authorize(request, corpId);
            if (!authorization.Allowed) return null;

            var collection = AggregateService.GetCollection("FormData");
            var filter = WrapFilter(request.Filter, request.DataSource.Id, authorization.CorpId);
            filter = filter.And(authorization.DataFilter)!;
            request.Filter = filter;

            var pipeline = PipelineDefinition<BsonDocument, BsonDocument>.Create(
                PipelineBuilder.BuildPipeline(collection, request, ServiceContext));
            return await collection.AggregateAsync(pipeline);
        }

        public async Task<long> Count(AggCalcRequest request)
        {
            return await Count(request, ServiceContext.CorpId);
        }

        public async Task<long> Count(AggCalcRequest request, string corpId)
        {
            if (request.DataSource?.Type != AgDataSourceType.Form) return 0;
            var authorization = Authorize(request, corpId);
            if (!authorization.Allowed) return 0;

            var collection = AggregateService.GetCollection("FormData");
            var filter = WrapFilter(request.Filter, request.DataSource.Id, authorization.CorpId);
            filter = filter.And(authorization.DataFilter)!;
            var filterDef = filter.ToFilterDefinition<BsonDocument>();
            return await collection.CountDocumentsAsync(filterDef);
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

    static class PipelineBuilder
    {
        public static BsonDocument[] BuildPipeline(IMongoCollection<BsonDocument> collection, AggCalcRequest request, IServiceContext context)
        {
            var pipelineStages = new List<BsonDocument>();

            if (request.Filter != null)
            {
                var filterDef = request.Filter.ToFilterDefinition<BsonDocument>();
                var matchStage = new BsonDocument("$match", filterDef.Render(new RenderArgs<BsonDocument>(collection.DocumentSerializer, BsonSerializer.SerializerRegistry)));
                pipelineStages.Add(matchStage);
            }

            if (request.Metrics?.Count > 0)
            {
                pipelineStages.Add(BuildGroupStage(request.Dimensions, request.Metrics));
                if (request.Dimensions?.Count > 0)
                {
                    pipelineStages.Add(BuildProjectStage(request.Dimensions!, request.Metrics!));
                }
            }

            if (request.Sort?.Count > 0)
            {
                pipelineStages.Add(BuildSortStage(request.Sort!));
            }

            if (request.Skip.HasValue && request.Skip.Value > 0)
            {
                pipelineStages.Add(new BsonDocument("$skip", request.Skip.Value));
            }

            if (request.Take.HasValue && request.Take.Value > 0)
            {
                pipelineStages.Add(new BsonDocument("$limit", request.Take.Value));
            }

            // 公开模式：按 displayFields 投影
            if (request.DisplayFields?.Count > 0)
            {
                pipelineStages.Add(BuildDisplayFieldProjectStage(request.DisplayFields));
            }

            return pipelineStages.ToArray();
        }

        private static BsonDocument BuildGroupStage(List<Dimension>? dimensions, List<Metric> metrics)
        {
            var groupDoc = new BsonDocument();

            if (dimensions != null && dimensions.Any())
            {
                var idDoc = new BsonDocument();
                foreach (var dimension in dimensions)
                {
                    if (!string.IsNullOrEmpty(dimension.Id))
                    {
                        var finalId = GetFinalId(dimension.Id);
                        idDoc[dimension.Id] = $"${finalId}";
                    }
                }
                groupDoc["_id"] = idDoc;
            }
            else
            {
                groupDoc["_id"] = BsonNull.Value;
            }

            foreach (var metric in metrics)
            {
                if (string.IsNullOrEmpty(metric.Id) || string.IsNullOrEmpty(metric.AggFun))
                    continue;

                if (metric.AggFun.Equals("count", StringComparison.OrdinalIgnoreCase))
                {
                    groupDoc[$"{metric.Id}_count"] = new BsonDocument("$sum", 1);
                }
                else
                {
                    var finalId = GetFinalId(metric.Id);
                    groupDoc[$"{metric.Id}_{metric.AggFun}"] =
                        new BsonDocument($"${metric.AggFun}", $"${finalId}");
                }
            }

            return new BsonDocument("$group", groupDoc);
        }

        private static BsonDocument BuildProjectStage(List<Dimension> dimensions, List<Metric> metrics)
        {
            var projectDoc = new BsonDocument();

            foreach (var dimension in dimensions)
            {
                if (!string.IsNullOrEmpty(dimension.Id))
                {
                    projectDoc[dimension.Id] = $"$_id.{dimension.Id}";
                }
            }

            foreach (var metric in metrics)
            {
                if (string.IsNullOrEmpty(metric.Id) || string.IsNullOrEmpty(metric.AggFun))
                    continue;

                projectDoc[$"{metric.Id}_{metric.AggFun}"] = $"${metric.Id}_{metric.AggFun}";
            }

            projectDoc["_id"] = 0;
            return new BsonDocument("$project", projectDoc);
        }

        private static BsonDocument BuildDisplayFieldProjectStage(List<string> displayFields)
        {
            var projectDoc = new BsonDocument
            {
                ["_id"] = 0,
                [Fields.Id] = 1,
                [Fields.AppId] = 1,
                [Fields.FormId] = 1,
                [Fields.DataTitle] = 1,
                [Fields.CreateTime] = 1,
            };

            foreach (var field in displayFields)
            {
                if (string.IsNullOrWhiteSpace(field)) continue;
                var root = field.Split('.', 2)[0];
                projectDoc[$"{Fields.Data}.{root}"] = 1;
            }

            return new BsonDocument("$project", projectDoc);
        }

        private static BsonDocument BuildSortStage(List<SortItem> sort)
        {
            var sortDoc = new BsonDocument();
            foreach (var rule in sort)
            {
                if (string.IsNullOrEmpty(rule.Id)) continue;
                sortDoc[rule.Id] = (int)rule.Dir;
            }
            return new BsonDocument("$sort", sortDoc);
        }

        private static string GetFinalId(string field)
        {
            return Fields.IsSystemField(field) ? field : $"data.{field}";
        }
    }
}
