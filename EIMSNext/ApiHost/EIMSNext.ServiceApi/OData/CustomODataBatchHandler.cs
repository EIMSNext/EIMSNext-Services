using EIMSNext.ApiService;
using Microsoft.AspNetCore.OData.Batch;

namespace EIMSNext.ServiceApi.OData
{
    /// <summary>
    /// 
    /// </summary>
    public class CustomODataBatchHandler : DefaultODataBatchHandler
    {
        /// <summary>
        /// 
        /// </summary>
        public CustomODataBatchHandler()
        {
            MessageQuotas.MaxNestingDepth = 2;
            MessageQuotas.MaxOperationsPerChangeset = 20;
            MessageQuotas.MaxReceivedMessageSize = 100;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override Task<IList<ODataBatchRequestItem>> ParseBatchRequestsAsync(HttpContext context)
        {
            //此处实例化IdentityContext，以使得整得batch执行过程中可以正确获取当前用户信息
            //默认odatabatch请求的httpcontextaccessor是拿不到httpcontext对象的， Odata bug?
            context.RequestServices.GetRequiredService<IIdentityContext>();
            return base.ParseBatchRequestsAsync(context);
        }
    }
}
