using Asp.Versioning;

using EIMSNext.ServiceApi.Extension;
using EIMSNext.Common;

using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace EIMSNext.ServiceApi.Controllers
{
    /// <summary>
    /// 所有方法都允许匿名访问，无需登录
    /// </summary>
    [ApiController, ApiVersion(1.0), ApiVersion(2.0)]
    public class OpenController : ControllerBase
    {
        /// <summary>
        /// test if works
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("api/v{version:apiVersion}/ping")]
        public IActionResult Ping()
        {
            return ApiResult.Success("API Server is running.").ToActionResult();
        }

        [Route("api/version"), HttpGet]
        public string Version()
        {
            return Assembly.GetExecutingAssembly().GetName().Version!.ToString();
        }
    }
}
