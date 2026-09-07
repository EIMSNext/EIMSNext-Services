using EIMSNext.ApiService.RequestModels;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EIMSNext.ApiService
{
    /// <summary>
    /// 聚合计算 API 服务接口。
    /// </summary>
    public interface IAggregateApiService: IApiService
    {
        /// <summary>
        /// 执行聚合计算。
        /// </summary>
        /// <param name="request">聚合计算请求。</param>
        /// <returns>聚合结果游标。</returns>
        Task<IAsyncCursor<BsonDocument>?> Calucate(AggCalcRequest request);

        /// <summary>
        /// 按企业维度执行聚合计算。
        /// </summary>
        /// <param name="request">聚合计算请求。</param>
        /// <param name="corpId">企业 ID。</param>
        /// <returns>聚合结果游标。</returns>
        Task<IAsyncCursor<BsonDocument>?> Calucate(AggCalcRequest request, string corpId);

        /// <summary>
        /// 统计聚合计算结果数量。
        /// </summary>
        /// <param name="request">聚合计算请求。</param>
        /// <returns>结果数量。</returns>
        Task<long> Count(AggCalcRequest request);

        /// <summary>
        /// 按企业维度统计聚合计算结果数量。
        /// </summary>
        /// <param name="request">聚合计算请求。</param>
        /// <param name="corpId">企业 ID。</param>
        /// <returns>结果数量。</returns>
        Task<long> Count(AggCalcRequest request, string corpId);

        /// <summary>
        /// 执行仪表盘聚合计算。
        /// </summary>
        /// <param name="request">仪表盘聚合请求。</param>
        /// <returns>聚合结果游标。</returns>
        Task<IAsyncCursor<BsonDocument>?> Calucate(DashboardAggregateRequest request);

        /// <summary>
        /// 统计仪表盘聚合结果数量。
        /// </summary>
        /// <param name="request">仪表盘聚合请求。</param>
        /// <returns>结果数量。</returns>
        Task<long> Count(DashboardAggregateRequest request);

        /// <summary>
        /// 预览仪表盘聚合结果。
        /// </summary>
        /// <param name="request">仪表盘聚合预览请求。</param>
        /// <returns>预览结果游标。</returns>
        Task<IAsyncCursor<BsonDocument>?> Preview(DashboardAggregatePreviewRequest request);

        /// <summary>
        /// 统计仪表盘聚合预览结果数量。
        /// </summary>
        /// <param name="request">仪表盘聚合预览请求。</param>
        /// <returns>预览结果数量。</returns>
        Task<long> PreviewCount(DashboardAggregatePreviewRequest request);
    }
}
