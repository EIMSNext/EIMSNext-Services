using EIMSNext.ApiService.RequestModel;
using EIMSNext.Common;
using EIMSNext.Core.Entity;
using EIMSNext.Core.Query;
using EIMSNext.Core.Service;
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
            IMongoCollection<BsonDocument> collection;
            if (request.DataSource!.Type == AgDataSourceType.Form)
            {
                collection = AggregateService.GetCollection("FormData");

                var filter = request.Filter;
                if (filter == null) { filter = new DynamicFilter(); }
                if (filter.IsGroup && filter.Rel == FilterRel.And)
                {
                    filter.Items!.Add(new DynamicFilter() { Field = Fields.CorpId, Op = FilterOp.Eq, Value = ServiceContext.CorpId });
                    filter.Items!.Add(new DynamicFilter() { Field = Fields.FormId, Op = FilterOp.Eq, Value = request.DataSource.Id });
                }
                else
                {
                    filter = new DynamicFilter()
                    {
                        Rel = FilterRel.And,
                        Items = [
                            new DynamicFilter() { Field = Fields.CorpId, Op = FilterOp.Eq, Value = ServiceContext.CorpId },
                            new DynamicFilter() { Field = Fields.FormId, Op = FilterOp.Eq, Value = request.DataSource.Id },
                            filter
                        ]
                    };
                }
                request.Filter = filter;

                var pipeline = PipelineDefinition<BsonDocument, BsonDocument>.Create(PipelineBuilder.BuildPipeline(collection, request, ServiceContext));
                return await collection.AggregateAsync(pipeline);
            }
            return null;
        }
    }

    static class PipelineBuilder
    {
        /// <summary>
        /// 根据AggCalcRequest动态构建MongoDB聚合管道
        /// </summary>
        /// <param name="request">聚合请求</param>
        /// <returns>构建好的BsonDocument[]管道</returns>
        /// <exception cref="ArgumentException">参数校验异常</exception>
        public static BsonDocument[] BuildPipeline(IMongoCollection<BsonDocument> collection, AggCalcRequest request, IServiceContext context)
        {
            var pipelineStages = new List<BsonDocument>();

            if (request.Filter != null)
            {
                var filterDef = request.Filter.ToFilterDefinition<BsonDocument>();
                var matchStage = new BsonDocument("$match", filterDef.Render(new RenderArgs<BsonDocument>(collection.DocumentSerializer, BsonSerializer.SerializerRegistry)));
                pipelineStages.Add(matchStage);
            }

            var groupStage = BuildGroupStage(request.Dimensions!, request.Metrics!);
            pipelineStages.Add(groupStage);

            if (request.Dimensions != null && request.Dimensions.Any())
            {
                var projectStage = BuildProjectStage(request.Dimensions!, request.Metrics!);
                pipelineStages.Add(projectStage);
            }

            return pipelineStages.ToArray();
        }

        #region 私有构建方法

        /// <summary>
        /// 构建$group分组聚合阶段
        /// </summary>
        private static BsonDocument BuildGroupStage(List<Dimension>? dimensions, List<Metric> metrics)
        {
            var groupDoc = new BsonDocument();

            // 构建分组键（_id）：单维度/多维度
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
                // 无维度：全局聚合（_id固定为null）
                groupDoc["_id"] = BsonNull.Value;
            }

            // 构建指标聚合（$sum/$avg/$count等）
            foreach (var metric in metrics)
            {
                if (string.IsNullOrEmpty(metric.Id) || string.IsNullOrEmpty(metric.AgFun))
                    continue;

                // 处理特殊聚合函数：$count（无需字段名）
                if (metric.AgFun.Equals("count", StringComparison.OrdinalIgnoreCase))
                {
                    groupDoc[$"{metric.Id}_count"] = new BsonDocument("$sum", 1);
                }
                else
                {
                    var finalId = GetFinalId(metric.Id);
                    // 常规聚合函数：$sum/$avg/$max/$min
                    groupDoc[$"{metric.Id}_{metric.AgFun}"] =
                        new BsonDocument($"${metric.AgFun}", $"${finalId}");
                }
            }

            return new BsonDocument("$group", groupDoc);
        }
        private static string GetFinalId(string field)
        {
            return Fields.IsSystemField(field) ? field : $"data.{field}";
        }
        /// <summary>
        /// 构建$project阶段（将_id中的维度字段平级输出）
        /// </summary>
        private static BsonDocument BuildProjectStage(List<Dimension> dimensions, List<Metric> metrics)
        {
            var projectDoc = new BsonDocument();

            // 1. 平级输出维度字段（从_id中提取）
            foreach (var dimension in dimensions)
            {
                if (!string.IsNullOrEmpty(dimension.Id))
                {
                    projectDoc[dimension.Id] = $"$_id.{dimension.Id}";
                }
            }

            // 2. 保留聚合指标字段
            foreach (var metric in metrics)
            {
                if (string.IsNullOrEmpty(metric.Id) || string.IsNullOrEmpty(metric.AgFun))
                    continue;

                projectDoc[$"{metric.Id}_{metric.AgFun}"] = $"${metric.Id}_{metric.AgFun}";
            }

            // 3. 隐藏默认的_id字段
            projectDoc["_id"] = 0;

            return new BsonDocument("$project", projectDoc);
        }
        #endregion

    }
}
