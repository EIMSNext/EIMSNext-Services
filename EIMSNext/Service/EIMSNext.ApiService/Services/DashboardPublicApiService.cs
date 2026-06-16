using EIMSNext.ApiService.RequestModels;
using EIMSNext.Common;
using EIMSNext.Core;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Query;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EIMSNext.ApiService
{
    public class DashboardPublicApiService(IResolver resolver) : ApiServiceBase(resolver)
    {
        public DashboardPublicPayload? GetDashboard(string token)
        {
            var dashboard = ResolvePublicDashboard(token);
            if (dashboard == null)
            {
                return null;
            }

            var items = Resolver.GetService<DashboardItemDef>()
                .All()
                .Where(x => x.CorpId == dashboard.CorpId && !x.DeleteFlag && x.DashboardId == dashboard.Id)
                .ToList();

            return new DashboardPublicPayload
            {
                Dashboard = dashboard,
                Items = items,
            };
        }

        public async Task<IReadOnlyList<BsonDocument>?> CalculateChart(string token, string itemId, AggCalcRequest request)
        {
            var context = ResolvePublicItemContext(token, itemId);
            if (context == null || request.DataSource == null || request.DataSource.Id != context.Value.FormId)
            {
                return null;
            }

            var cursor = await Resolver.Resolve<IAggregateApiService>().Calucate(request, context.Value.Dashboard.CorpId ?? string.Empty);
            return cursor == null ? null : await cursor.ToListAsync();
        }

        public long? CountData(string token, DashboardPublicDataRequest request)
        {
            var context = ResolvePublicItemContext(token, request.ItemId);
            if (context == null)
            {
                return null;
            }

            var options = BuildPublicFindOptions(context.Value, request.Options);
            return Resolver.GetService<IFormDataService, FormData>().Count(options.Filter ?? DynamicFilter.Empty);
        }

        public IReadOnlyList<FormData>? QueryData(string token, DashboardPublicDataRequest request)
        {
            var context = ResolvePublicItemContext(token, request.ItemId);
            if (context == null)
            {
                return null;
            }

            var options = BuildPublicFindOptions(context.Value, request.Options);
            return Resolver.GetService<IFormDataService, FormData>().Find(options).ToList();
        }

        public async Task<FormDataFilterOptionsResponse?> GetFilterOptions(string token, DashboardPublicFilterOptionsRequest request)
        {
            var context = ResolvePublicItemContext(token, request.ItemId);
            if (context == null || request.Options.FormId != context.Value.FormId)
            {
                return null;
            }

            var field = DynamicField.FormatFieldForFilter($"data.{request.Options.Field}", request.Options.FieldType);
            var limit = request.Options.Limit <= 0 ? 50 : Math.Min(request.Options.Limit, 200);
            var result = await Resolver.GetService<IFormDataService, FormData>().GetFieldOptionsAsync(new FilterOptionQuery
            {
                Filter = BuildPublicFilter(context.Value, request.Options.Filter),
                FieldPath = field,
                Keyword = request.Options.Keyword,
                Limit = limit,
            });

            return new FormDataFilterOptionsResponse { Items = result.Items };
        }

        private DashboardDef? ResolvePublicDashboard(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            return Resolver.GetService<DashboardDef>()
                .All()
                .FirstOrDefault(x => !x.DeleteFlag && x.PublicEnabled && x.PublicToken == token);
        }

        private (DashboardDef Dashboard, DashboardItemDef Item, string FormId)? ResolvePublicItemContext(string token, string itemId)
        {
            var dashboard = ResolvePublicDashboard(token);
            if (dashboard == null || string.IsNullOrWhiteSpace(itemId))
            {
                return null;
            }

            var item = Resolver.GetService<DashboardItemDef>().Get(itemId);
            if (item == null || item.DeleteFlag || item.CorpId != dashboard.CorpId || item.DashboardId != dashboard.Id)
            {
                return null;
            }

            var formId = ResolveItemFormId(item);
            if (string.IsNullOrWhiteSpace(formId))
            {
                return null;
            }

            return (dashboard, item, formId);
        }

        private static string? ResolveItemFormId(DashboardItemDef item)
        {
            if (string.IsNullOrWhiteSpace(item.Details))
            {
                return null;
            }

            try
            {
                var doc = BsonDocument.Parse(item.Details);
                if (!doc.TryGetValue("datasource", out var datasourceValue) ||
                    !datasourceValue.IsBsonDocument ||
                    !datasourceValue.AsBsonDocument.TryGetValue("id", out var idValue))
                {
                    return null;
                }

                return idValue.IsString ? idValue.AsString : idValue.ToString();
            }
            catch
            {
                return null;
            }
        }

        private static DynamicFindOptions<FormData> BuildPublicFindOptions(
            (DashboardDef Dashboard, DashboardItemDef Item, string FormId) context,
            DynamicFindOptions<FormData>? options)
        {
            options ??= new DynamicFindOptions<FormData>();
            var baseFilter = BuildPublicFilter(context, null);
            options.Filter = options.Filter == null || options.Filter.IsEmpty
                ? baseFilter
                : new DynamicFilter { Rel = FilterRel.And, Items = [baseFilter, options.Filter] };
            options.Scope = null;
            options.Take = Math.Clamp(options.Take <= 0 ? 20 : options.Take, 1, 200);
            options.Skip = Math.Max(0, options.Skip);
            return options;
        }

        private static DynamicFilter BuildPublicFilter(
            (DashboardDef Dashboard, DashboardItemDef Item, string FormId) context,
            DynamicFilter? filter)
        {
            var baseFilter = new DynamicFilter
            {
                Rel = FilterRel.And,
                Items =
                [
                    new DynamicFilter { Field = Fields.CorpId, Op = FilterOp.Eq, Value = context.Dashboard.CorpId },
                    new DynamicFilter { Field = Fields.FormId, Op = FilterOp.Eq, Value = context.FormId },
                    new DynamicFilter { Field = Fields.DeleteFlag, Op = FilterOp.Ne, Value = true },
                ],
            };

            return filter == null || filter.IsEmpty
                ? baseFilter
                : new DynamicFilter { Rel = FilterRel.And, Items = [baseFilter, filter] };
        }
    }
}
