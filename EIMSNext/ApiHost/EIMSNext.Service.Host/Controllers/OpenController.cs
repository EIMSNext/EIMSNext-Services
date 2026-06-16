using Asp.Versioning;

using EIMSNext.ApiHost.Extensions;
using EIMSNext.ApiService;
using EIMSNext.Common;
using EIMSNext.Core;
using EIMSNext.Service.Contracts;
using HKH.Mef2.Integration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using EIMSNext.ApiService.RequestModels;

namespace EIMSNext.Service.Host.Controllers
{
    /// <summary>
    /// 所有方法都允许匿名访问，无需登录
    /// </summary>
    [ApiController, ApiVersion(1.0), ApiVersion(2.0)]
    public class OpenController(IResolver resolver) : ControllerBase
    {
        private readonly AppStoreApiService _appStoreApiService = resolver.Resolve<AppStoreApiService>();
        private readonly DashboardPublicApiService _dashboardPublicApiService = resolver.Resolve<DashboardPublicApiService>();
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
            var payload = _dashboardPublicApiService.GetDashboard(token);
            if (payload == null)
            {
                return NotFound();
            }

            return ApiResult.Success(payload).ToActionResult();
        }

        [HttpPost("api/v{version:apiVersion}/open/dashboard/{token}/chart")]
        public async Task<IActionResult> CalculateChart(string token, [FromBody] AggCalcRequest request, [FromQuery] string itemId)
        {
            var result = await _dashboardPublicApiService.CalculateChart(token, itemId, request);
            if (result == null)
            {
                return NotFound();
            }

            return ApiResult.Success(result).ToActionResult();
        }

        [HttpPost("api/v{version:apiVersion}/open/dashboard/{token}/data/count")]
        public IActionResult CountData(string token, [FromBody] DashboardPublicDataRequest request)
        {
            var count = _dashboardPublicApiService.CountData(token, request);
            return count == null ? NotFound() : Ok(count.Value);
        }

        [HttpPost("api/v{version:apiVersion}/open/dashboard/{token}/data/query")]
        public IActionResult QueryData(string token, [FromBody] DashboardPublicDataRequest request)
        {
            var result = _dashboardPublicApiService.QueryData(token, request);
            if (result == null)
            {
                return NotFound();
            }

            return Ok(new { value = result });
        }

        [HttpPost("api/v{version:apiVersion}/open/dashboard/{token}/filter/options")]
        public async Task<IActionResult> GetFilterOptions(string token, [FromBody] DashboardPublicFilterOptionsRequest request)
        {
            var result = await _dashboardPublicApiService.GetFilterOptions(token, request);
            if (result == null)
            {
                return NotFound();
            }

            return Ok(result);
        }
    }
}
