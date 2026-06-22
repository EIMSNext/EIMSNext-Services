using Asp.Versioning;

using EIMSNext.ApiHost.Extensions;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.Common;
using EIMSNext.Core;
using EIMSNext.Service.Contracts;
using HKH.Mef2.Integration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace EIMSNext.Service.Host.Controllers
{
    /// <summary>
    /// 所有方法都允许匿名访问，无需登录
    /// </summary>
    [ApiController, ApiVersion(1.0), ApiVersion(2.0)]
    public class OpenController(IResolver resolver) : ControllerBase
    {
        private readonly AppStoreApiService _appStoreApiService = resolver.Resolve<AppStoreApiService>();
        private readonly IAppInstallService _appInstallService = resolver.Resolve<IAppInstallService>();

        /// <summary>
        /// test if works
        /// </summary>
        /// <returns></returns>
        [HttpGet("api/v{version:apiVersion}/Ping")]
        public IActionResult Ping()
        {
            return ApiResult.Success("API Server is running.").ToActionResult();
        }

        [HttpGet("api/Version")]
        public string Version()
        {
            return Assembly.GetExecutingAssembly().GetName().Version!.ToString();
        }

        [HttpGet("api/v{version:apiVersion}/open/appstore")]
        public IActionResult GetAppStore([FromQuery] AppProfileQueryRequest request)
        {
            var (total, items) = _appStoreApiService.GetAppStore(request);
            return ApiResult.Success(new { total, items }).ToActionResult();
        }

        [HttpGet("api/v{version:apiVersion}/open/appstore/{id}")]
        public IActionResult GetAppStoreDetail(string id)
        {
            var profile = _appStoreApiService.GetAppStoreDetail(id);
            return profile == null ? NotFound() : ApiResult.Success(profile).ToActionResult();
        }

        [HttpPost("api/v{version:apiVersion}/open/appstore/{id}/install")]
        [Authorize]
        public async Task<IActionResult> Install(string id)
        {
            var appId = await _appInstallService.InstallAsync(id);
            return ApiResult.Success(new { appId }).ToActionResult();
        }

        [HttpGet("api/v{version:apiVersion}/open/dashboard/{token}")]
        public IActionResult GetDashboard(string token)
        {
            return DashboardPublicApiGone();
        }

        [HttpPost("api/v{version:apiVersion}/open/dashboard/{token}/chart")]
        public IActionResult CalculateChart(string token)
        {
            return DashboardPublicApiGone();
        }

        [HttpPost("api/v{version:apiVersion}/open/dashboard/{token}/data/count")]
        public IActionResult CountData(string token)
        {
            return DashboardPublicApiGone();
        }

        [HttpPost("api/v{version:apiVersion}/open/dashboard/{token}/data/query")]
        public IActionResult QueryData(string token)
        {
            return DashboardPublicApiGone();
        }

        [HttpPost("api/v{version:apiVersion}/open/dashboard/{token}/filter/options")]
        public IActionResult GetFilterOptions(string token)
        {
            return DashboardPublicApiGone();
        }

        private IActionResult DashboardPublicApiGone()
        {
            return StatusCode(StatusCodes.Status410Gone, "公开仪表盘匿名接口已关闭，请通过 public/token 获取 Public 身份后访问普通接口。");
        }
    }
}
