using System.Reflection;

using Asp.Versioning;

using EIMSNext.ApiHost.Extensions;
using EIMSNext.ApiCore;
using EIMSNext.ApiService;
using EIMSNext.Common;

using HKH.Mef2.Integration;

using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.Flow.Host.Controllers
{
    /// <summary>
    /// 所有方法都允许匿名访问，无需登录
    /// </summary>
    [ApiController, ApiVersion(1.0), ApiVersion(2.0)]
    public class OpenController : ControllerBase
    {
        private readonly IResolver _resolver;

        public OpenController(IResolver resolver)
        {
            _resolver = resolver;
        }

        /// <summary>
        /// test if works
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("api/v{version:apiVersion}/ping")]
        public IActionResult Ping()
        {
            return ApiResult.Success("Workflow API Server is running.").ToActionResult();
        }

        [Route("api/version"), HttpGet]
        public string Version()
        {
            return Assembly.GetExecutingAssembly().GetName().Version!.ToString();
        }

        /// <summary>
        /// 通过HTTP触发智能助手。
        /// </summary>
        [HttpPost]
        [Route("api/v{version:apiVersion}/tenant/{corpId}/dataflow/{dataflowId}")]
        public async Task<IActionResult> TriggerDataflowAsync([FromRoute] string corpId, [FromRoute] string dataflowId)
        {
            var accessor = _resolver.Resolve<IHttpContextAccessor>();
            var clientIp = IpHelper.GetClientIp(accessor);
            var headers = Request.Headers.ToDictionary(x => x.Key, x => x.Value.ToString());

            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            var result = await _resolver.Resolve<DataflowHookApiService>().HandleAsync(
                corpId,
                dataflowId,
                clientIp,
                Request.Method,
                Request.ContentType ?? string.Empty,
                headers,
                body);

            return Content(result.Body, result.ContentType, System.Text.Encoding.UTF8);
        }
    }
}
