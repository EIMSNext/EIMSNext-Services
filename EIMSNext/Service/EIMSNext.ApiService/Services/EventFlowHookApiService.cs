using EIMSNext.Service.Contracts;
using EIMSNext.Entities;

using HKH.Mef2.Integration;

namespace EIMSNext.ApiService
{
    /// <summary>
    /// 数据流HTTP开放接口应用服务。
    /// </summary>
    public class EventFlowHookApiService(IResolver resolver)
    {
        private readonly IEventFlowHookService _service = resolver.Resolve<IEventFlowHookService>();

        /// <summary>
        /// 执行HTTP触发。
        /// </summary>
        public Task<(int StatusCode, string ContentType, string Body)> HandleAsync(string corpId, string eventFlowId, string clientIp, string method, string contentType, Dictionary<string, string> headers, string body)
        {
            return _service.HandleAsync(corpId, eventFlowId, clientIp, method, contentType, headers, body);
        }

        /// <summary>
        /// 获取最近一次HTTP样例。
        /// </summary>
        public Task<EventFlowHookSample?> GetLatestSampleAsync(string corpId, string eventFlowId)
        {
            return _service.GetLatestSampleAsync(corpId, eventFlowId);
        }
    }
}
