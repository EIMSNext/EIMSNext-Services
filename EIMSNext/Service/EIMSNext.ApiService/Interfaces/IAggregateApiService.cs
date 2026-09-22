using EIMSNext.ApiService.RequestModels;

namespace EIMSNext.ApiService
{
    /// <summary>
    /// 聚合计算 API 服务接口。
    /// </summary>
    /// <remarks>
    /// PostgreSQL 迁移后本接口不再返回 Mongo 游标：
    /// <c>IAsyncCursor&lt;BsonDocument&gt;</c> 换为 <see cref="Dictionary{TKey,TValue}"/> 列表，
    /// 聚合由 SQL <c>group by</c> 直接完成。
    /// </remarks>
    public interface IAggregateApiService : IApiService
    {
        /// <summary>
        /// 执行聚合计算。
        /// </summary>
        /// <param name="request">聚合计算请求。</param>
        /// <returns>聚合结果行；无数据时为 null。</returns>
        Task<List<Dictionary<string, object?>>?> Calucate(AggCalcRequest request);

        /// <summary>
        /// 按企业维度执行聚合计算。
        /// </summary>
        /// <param name="request">聚合计算请求。</param>
        /// <param name="corpId">企业 ID。</param>
        /// <returns>聚合结果行；无数据时为 null。</returns>
        Task<List<Dictionary<string, object?>>?> Calucate(AggCalcRequest request, string corpId);

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
        /// <returns>聚合结果行；无数据时为 null。</returns>
        Task<List<Dictionary<string, object?>>?> Calucate(DashboardAggregateRequest request);

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
        /// <returns>预览结果行；无数据时为 null。</returns>
        Task<List<Dictionary<string, object?>>?> Preview(DashboardAggregatePreviewRequest request);

        /// <summary>
        /// 统计仪表盘聚合预览结果数量。
        /// </summary>
        /// <param name="request">仪表盘聚合预览请求。</param>
        /// <returns>预览结果数量。</returns>
        Task<long> PreviewCount(DashboardAggregatePreviewRequest request);
    }
}
