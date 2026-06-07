using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;

using HKH.Mef2.Integration;

namespace EIMSNext.ApiService
{
    /// <summary>
    /// 数据流HTTP开放接口应用服务。
    /// </summary>
    public class DataflowHookApiService(IResolver resolver)
    {
        private readonly IDataflowHookService _service = resolver.Resolve<IDataflowHookService>();

        /// <summary>
        /// 执行HTTP触发。
        /// </summary>
        public Task<(int StatusCode, string ContentType, string Body)> HandleAsync(string corpId, string dataflowId, string clientIp, string method, string contentType, Dictionary<string, string> headers, string body)
        {
            return _service.HandleAsync(corpId, dataflowId, clientIp, method, contentType, headers, body);
        }

        /// <summary>
        /// 获取最近一次HTTP样例。
        /// </summary>
        public Task<DataflowHookSample?> GetLatestSampleAsync(string corpId, string dataflowId)
        {
            return _service.GetLatestSampleAsync(corpId, dataflowId);
        }
    }
}
