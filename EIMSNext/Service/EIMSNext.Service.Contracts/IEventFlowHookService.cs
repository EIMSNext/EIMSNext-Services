using EIMSNext.Service.Entities;

namespace EIMSNext.Service.Contracts
{
    /// <summary>
    /// 数据流HTTP触发服务。
    /// </summary>
    public interface IEventFlowHookService
    {
        /// <summary>
        /// 处理HTTP触发请求。
        /// </summary>
        Task<(int StatusCode, string ContentType, string Body)> HandleAsync(string corpId, string eventFlowId, string clientIp, string method, string contentType, Dictionary<string, string> headers, string body);

        /// <summary>
        /// 获取指定数据流最近一次样例。
        /// </summary>
        Task<EventFlowHookSample?> GetLatestSampleAsync(string corpId, string eventFlowId);
    }
}
