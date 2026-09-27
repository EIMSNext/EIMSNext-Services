using Asp.Versioning;

using EIMSNext.ApiHost.Extensions;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Host.Authorization;
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
        [AllowAnonymous]
        public IActionResult Ping()
        {
            return ApiResult.Success("API Server is running.").ToActionResult();
        }

        [HttpGet("api/Version")]
        [AllowAnonymous]
        public string Version()
        {
            return Assembly.GetExecutingAssembly().GetName().Version!.ToString();
        }

        [HttpGet("api/v{version:apiVersion}/open/appstore")]
        [AllowAnonymous]
        public IActionResult GetAppStore([FromQuery] AppProfileQueryRequest request)
        {
            var (total, items) = _appStoreApiService.GetAppStore(request);
            return ApiResult.Success(new { total, items }).ToActionResult();
        }

        [HttpGet("api/v{version:apiVersion}/open/appstore/{id}")]
        [AllowAnonymous]
        public IActionResult GetAppStoreDetail(string id)
        {
            var profile = _appStoreApiService.GetAppStoreDetail(id);
            return profile == null ? NotFound() : ApiResult.Success(profile).ToActionResult();
        }

        [HttpPost("api/v{version:apiVersion}/open/appstore/{id}/install")]
        [Authorize]
        [IdentityType(IdentityType.Corp_Admins)]
        public async Task<IActionResult> Install(string id)
        {
            var appId = await _appInstallService.InstallAsync(id);
            return ApiResult.Success(new { appId }).ToActionResult();
        }

    }
}
