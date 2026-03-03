using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EIMSNext.ApiService.RequestModel;
using EIMSNext.Core.Service;
using HKH.Mef2.Integration;
using MongoDB.Bson;

namespace EIMSNext.ApiService
{
    public class AggregateApiService : ApiServiceBase
    {
        public AggregateApiService(IResolver resolver) : base(resolver)
        {
            AggregateService = resolver.Resolve<AggregateService>();
        }

        private AggregateService AggregateService { get; set; }

        public dynamic Calucate(AggCalcRequest request)
        {
            if (request.DataSource!.Type == AgDataSourceType.Form)
            {

            }
            return new { };
            //AggregateService.Calucate
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
        public static BsonDocument[] BuildPipeline(AggCalcRequest request)
        {
            var pipelineStages = new List<BsonDocument>();

            if (request.Filter != null && request.Filter.Any())
            {
                var matchStage = BuildMatchStage(request.Filter);
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
        /// 构建$match过滤阶段
        /// </summary>
        private static BsonDocument BuildMatchStage(List<AgFilter> filters)
        {
            var matchFilter = new BsonDocument();
            foreach (var filter in filters)
            {
                if (string.IsNullOrEmpty(filter.Field) || string.IsNullOrEmpty(filter.Operator))
                    continue; // 跳过无效过滤条件

                // 支持常见操作符：$eq/$gt/$lt/$gte/$lte/$in/$nin
                matchFilter[filter.Field] = new BsonDocument(filter.Operator, filter.Value);
            }
            return new BsonDocument("$match", matchFilter);
        }

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
                        // 关联字段：$ + 维度名（如$category）
                        idDoc[dimension.Id] = "$" + dimension.Id;
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
                    continue; // 跳过无效指标

                // 处理特殊聚合函数：$count（无需字段名）
                if (metric.AgFun.Equals("$count", StringComparison.OrdinalIgnoreCase))
                {
                    groupDoc[metric.Id + "_count"] = new BsonDocument("$count", new BsonDocument());
                }
                else
                {
                    // 常规聚合函数：$sum/$avg/$max/$min
                    groupDoc[metric.Id + "_" + metric.AgFun.TrimStart('$')] =
                        new BsonDocument(metric.AgFun, "$" + metric.Id);
                }
            }

            return new BsonDocument("$group", groupDoc);
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
                    projectDoc[dimension.Id] = "$_id." + dimension.Id;
                }
            }

            // 2. 保留聚合指标字段
            foreach (var metric in metrics)
            {
                if (string.IsNullOrEmpty(metric.Id) || string.IsNullOrEmpty(metric.AgFun))
                    continue;

                var metricFieldName = metric.AgFun.Equals("$count", StringComparison.OrdinalIgnoreCase)
                    ? metric.Id + "_count"
                    : metric.Id + "_" + metric.AgFun.TrimStart('$');

                projectDoc[metricFieldName] = 1;
            }

            // 3. 隐藏默认的_id字段
            projectDoc["_id"] = 0;

            return new BsonDocument("$project", projectDoc);
        }
        #endregion

    }
}
