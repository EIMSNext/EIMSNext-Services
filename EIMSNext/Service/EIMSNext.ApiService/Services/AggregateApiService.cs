using EIMSNext.ApiService.RequestModels;
using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services;
using HKH.Mef2.Integration;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace EIMSNext.ApiService
{
    public class AggregateApiService : ApiServiceBase, IAggregateApiService
    {
        public AggregateApiService(IResolver resolver) : base(resolver)
        {
            AggregateService = resolver.Resolve<AggregateService>();
        }

        private AggregateService AggregateService { get; set; }

        public async Task<IAsyncCursor<BsonDocument>?> Calucate(AggCalcRequest request)
        {
            return await Calucate(request, ServiceContext.CorpId);
        }

        public async Task<IAsyncCursor<BsonDocument>?> Calucate(AggCalcRequest request, string corpId)
        {
            if (request.DataSource?.Type != AgDataSourceType.Form) return null;

            if (IdentityContext.IdentityType == IdentityType.Public)
            {
                var validator = Resolver.Resolve<IPublicAccessValidator>();
                if (!validator.CanReadDashboardItem(request.ItemId ?? string.Empty))
                    return null;
                if (!validator.CanReadDashboardForm(request.DataSource.Id))
                    return null;
                corpId = validator.GetCurrentSetting()?.CorpId ?? string.Empty;
            }

            var collection = AggregateService.GetCollection("FormData");
            var filter = WrapFilter(request.Filter, request.DataSource.Id, corpId);
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

            if (IdentityContext.IdentityType == IdentityType.Public)
            {
                var validator = Resolver.Resolve<IPublicAccessValidator>();
                if (!validator.CanReadDashboardItem(request.ItemId ?? string.Empty))
                    return 0;
                if (!validator.CanReadDashboardForm(request.DataSource.Id))
                    return 0;
                corpId = validator.GetCurrentSetting()?.CorpId ?? string.Empty;
            }

            var collection = AggregateService.GetCollection("FormData");
            var filter = WrapFilter(request.Filter, request.DataSource.Id, corpId);
            var filterDef = filter.ToFilterDefinition<BsonDocument>();
            return await collection.CountDocumentsAsync(filterDef);
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
