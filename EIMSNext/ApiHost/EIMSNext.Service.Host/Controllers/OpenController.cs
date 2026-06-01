using Asp.Versioning;

using EIMSNext.ApiHost.Extensions;
using EIMSNext.Common;
using EIMSNext.Core;
using EIMSNext.Core.Entities;
using EIMSNext.Service.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Host.Requests;
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
        private readonly IResolver _resolver = resolver;
        private readonly IRepository<AppProfile> _appProfileRepository = resolver.GetRepository<AppProfile>();
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
            var query = _appProfileRepository.Queryable.Where(x => !x.DeleteFlag);

            if (!string.IsNullOrWhiteSpace(request.Keyword))
            {
                query = query.Where(x => x.Name.Contains(request.Keyword) || x.Summary.Contains(request.Keyword) || x.Tags.Contains(request.Keyword));
            }

            if (!string.IsNullOrWhiteSpace(request.Category))
            {
                query = query.Where(x => x.Category == request.Category);
            }

            if (!string.IsNullOrWhiteSpace(request.Industry))
            {
                query = query.Where(x => x.Industry == request.Industry);
            }

            if (request.Recommended == true)
            {
                query = query.Where(x => x.IsRecommended);
            }

            var total = query.Count();
            var items = query.OrderByDescending(x => x.IsRecommended).ThenByDescending(x => x.SortIndex).Skip(request.Skip).Take(request.Take).ToList();
            return ApiResult.Success(new { total, items }).ToActionResult();
        }

        [HttpGet("api/v{version:apiVersion}/open/appstore/{id}")]
        public IActionResult GetAppStoreDetail(string id)
        {
            var profile = _appProfileRepository.Get(id);
            return profile == null ? NotFound() : ApiResult.Success(profile).ToActionResult();
        }

        [HttpPost("api/v{version:apiVersion}/open/appstore/{id}/install")]
        [Authorize]
        public async Task<IActionResult> Install(string id)
        {
            var appId = await _appInstallService.InstallAsync(id);
            return ApiResult.Success(new { appId }).ToActionResult();
        }

    }
}
