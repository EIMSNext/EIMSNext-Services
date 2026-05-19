using Asp.Versioning;

using EIMSNext.ApiHost.Extensions;
using EIMSNext.ApiService;
using EIMSNext.Common;
using EIMSNext.Core.Repositories;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;

using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace EIMSNext.Service.Host.Controllers
{
    /// <summary>
    /// 所有方法都允许匿名访问，无需登录
    /// </summary>
    [ApiController, ApiVersion(1.0), ApiVersion(2.0)]
    public class OpenController(IResolver resolver, IRepository<AppProfile> appProfileRepository, IIdentityContext identityContext, IAppInstallService appInstallService) : ControllerBase
    {
        private readonly IResolver _resolver = resolver;
        private readonly IRepository<AppProfile> _appProfileRepository = appProfileRepository;
        private readonly IIdentityContext _identityContext = identityContext;
        private readonly IAppInstallService _appInstallService = appInstallService;

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
        public IActionResult GetAppStore([FromQuery] string? keyword, [FromQuery] string? category, [FromQuery] string? industry, [FromQuery] bool? recommended, [FromQuery] int skip = 0, [FromQuery] int take = 24)
        {
            var query = _appProfileRepository.Queryable.Where(x => !x.DeleteFlag);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(x => x.Name.Contains(keyword) || x.Summary.Contains(keyword) || x.Tags.Contains(keyword));
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(x => x.Category == category);
            }

            if (!string.IsNullOrWhiteSpace(industry))
            {
                query = query.Where(x => x.Industry == industry);
            }

            if (recommended == true)
            {
                query = query.Where(x => x.IsRecommended);
            }

            var total = query.Count();
            var items = query.OrderByDescending(x => x.IsRecommended).ThenByDescending(x => x.SortIndex).Skip(skip).Take(take).ToList();
            return ApiResult.Success(new { total, items }).ToActionResult();
        }

        [HttpGet("api/v{version:apiVersion}/open/appstore/{id}")]
        public IActionResult GetAppStoreDetail(string id)
        {
            var profile = _appProfileRepository.Get(id);
            return profile == null ? NotFound() : ApiResult.Success(profile).ToActionResult();
        }

        [HttpPost("api/v{version:apiVersion}/open/appstore/{id}/install")]
        public async Task<IActionResult> Install(string id)
        {
            if (string.IsNullOrWhiteSpace(_identityContext.CurrentUserID) || string.IsNullOrWhiteSpace(_identityContext.CurrentCorpId))
            {
                return Unauthorized();
            }

            var appId = await _appInstallService.InstallAsync(id);
            return ApiResult.Success(new { appId }).ToActionResult();
        }
    }
}
