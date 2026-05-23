using Asp.Versioning;

using EIMSNext.ApiCore.Plugin;
using EIMSNext.ApiHost.Extensions;
using EIMSNext.ApiService;
using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Plugin.Contracts;
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
    public class OpenController(
        IResolver resolver,
        IRepository<AppProfile> appProfileRepository,
        IRepository<PluginProfile> pluginProfileRepository,
        IRepository<PluginInstall> pluginInstallRepository,
        IPluginRuntimeManager pluginRuntimeManager,
        IIdentityContext identityContext,
        IAppInstallService appInstallService) : ControllerBase
    {
        private readonly IResolver _resolver = resolver;
        private readonly IRepository<AppProfile> _appProfileRepository = appProfileRepository;
        private readonly IRepository<PluginProfile> _pluginProfileRepository = pluginProfileRepository;
        private readonly IRepository<PluginInstall> _pluginInstallRepository = pluginInstallRepository;
        private readonly IPluginRuntimeManager _pluginRuntimeManager = pluginRuntimeManager;
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

        [HttpGet("api/v{version:apiVersion}/open/pluginstore")]
        public IActionResult GetPluginStore([FromQuery] string? keyword, [FromQuery] string? category, [FromQuery] string? scenario, [FromQuery] bool? recommended, [FromQuery] int skip = 0, [FromQuery] int take = 24)
        {
            var query = _pluginProfileRepository.Queryable.Where(x => !x.DeleteFlag && x.Status == "Published");

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(x => x.Name.Contains(keyword) || x.Summary.Contains(keyword) || x.Tags.Contains(keyword));
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(x => x.Category == category);
            }

            if (!string.IsNullOrWhiteSpace(scenario))
            {
                query = query.Where(x => x.Scenario == scenario);
            }

            if (recommended == true)
            {
                query = query.Where(x => x.IsRecommended);
            }

            var corpId = _identityContext.CurrentCorpId;
            var installedPluginIds = string.IsNullOrWhiteSpace(corpId)
                ? []
                : _pluginInstallRepository.Queryable
                    .Where(x => x.CorpId == corpId && !x.DeleteFlag && x.Status == PluginInstallStatus.Installed)
                    .Select(x => x.PluginId)
                    .ToList();

            var total = query.Count();
            var items = query
                .OrderByDescending(x => x.IsRecommended)
                .ThenByDescending(x => x.SortIndex)
                .Skip(skip)
                .Take(take)
                .ToList()
                .Select(x => new
                {
                    x.Id,
                    x.PluginId,
                    x.Version,
                    x.Name,
                    x.Summary,
                    x.Description,
                    x.Icon,
                    x.CoverImage,
                    x.BannerImage,
                    x.GalleryImages,
                    x.Category,
                    x.Scenario,
                    x.Tags,
                    x.DeveloperName,
                    x.IsOfficial,
                    x.IsHot,
                    x.IsRecommended,
                    x.InstallCount,
                    x.SortIndex,
                    x.Status,
                    x.PublishedAt,
                    x.HelpDocUrl,
                    x.TemplateUrl,
                    x.PricingPlans,
                    installed = installedPluginIds.Contains(x.PluginId)
                })
                .ToList();

            return ApiResult.Success(new { total, items }).ToActionResult();
        }

        [HttpGet("api/v{version:apiVersion}/open/pluginstore/{id}")]
        public IActionResult GetPluginStoreDetail(string id)
        {
            var profile = _pluginProfileRepository.Get(id);
            if (profile == null || profile.DeleteFlag)
            {
                return NotFound();
            }

            var corpId = _identityContext.CurrentCorpId;
            var install = string.IsNullOrWhiteSpace(corpId)
                ? null
                : _pluginInstallRepository.Queryable.FirstOrDefault(x => x.CorpId == corpId && x.PluginId == profile.PluginId && !x.DeleteFlag && x.Status == PluginInstallStatus.Installed);
            var runtime = _pluginRuntimeManager.GetPlugins().FirstOrDefault(x => x.PluginId == profile.PluginId);
            var functions = profile.Functions.Count > 0
                ? profile.Functions.Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.Description,
                    inputFields = x.InputFields.ToList()
                })
                : runtime?.Functions.Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.Description,
                    inputFields = x.InputFields.ToList()
                }).ToList();

            return ApiResult.Success(new
            {
                profile.Id,
                profile.PluginId,
                profile.Version,
                profile.Name,
                profile.Summary,
                profile.Description,
                profile.Icon,
                profile.CoverImage,
                profile.BannerImage,
                profile.GalleryImages,
                profile.Category,
                profile.Scenario,
                profile.Tags,
                profile.DeveloperName,
                profile.IsOfficial,
                profile.IsHot,
                profile.IsRecommended,
                profile.InstallCount,
                profile.SortIndex,
                profile.Status,
                profile.PublishedAt,
                profile.HelpDocUrl,
                profile.TemplateUrl,
                profile.PricingPlans,
                functions,
                installed = install != null,
                installEnabled = install?.Enabled,
            }).ToActionResult();
        }

        [HttpPost("api/v{version:apiVersion}/open/pluginstore/{id}/install")]
        public async Task<IActionResult> InstallPlugin(string id)
        {
            if (string.IsNullOrWhiteSpace(_identityContext.CurrentUserID) || string.IsNullOrWhiteSpace(_identityContext.CurrentCorpId))
            {
                return Unauthorized();
            }

            var profile = _pluginProfileRepository.Get(id);
            if (profile == null || profile.DeleteFlag)
            {
                return NotFound();
            }

            var corpId = _identityContext.CurrentCorpId;
            var now = DateTime.UtcNow.ToTimeStampMs();
            var existing = _pluginInstallRepository.Queryable.FirstOrDefault(x => x.CorpId == corpId && x.PluginId == profile.PluginId && !x.DeleteFlag);
            if (existing == null)
            {
                existing = new PluginInstall
                {
                    Id = _pluginInstallRepository.NewId(),
                    CorpId = corpId,
                    PluginProfileId = profile.Id,
                    PluginId = profile.PluginId,
                    Version = profile.Version,
                    Name = profile.Name,
                    Summary = profile.Summary,
                    Icon = profile.Icon,
                    Status = PluginInstallStatus.Installed,
                    Enabled = true,
                    InstalledAt = now,
                    InstalledBy = _identityContext.CurrentEmployee?.ToOperator() ?? new Operator(_identityContext.CurrentUserID, _identityContext.CurrentUser?.Name ?? string.Empty, _identityContext.CurrentUser?.Name ?? string.Empty),
                    Source = "pluginstore"
                };
                await _pluginInstallRepository.InsertAsync(existing);
            }
            else
            {
                existing.PluginProfileId = profile.Id;
                existing.Version = profile.Version;
                existing.Name = profile.Name;
                existing.Summary = profile.Summary;
                existing.Icon = profile.Icon;
                existing.Status = PluginInstallStatus.Installed;
                existing.Enabled = true;
                existing.DeleteFlag = false;
                existing.UninstalledAt = null;
                existing.LastEnabledAt = now;
                await _pluginInstallRepository.ReplaceAsync(existing);
            }

            profile.InstallCount += 1;
            await _pluginProfileRepository.ReplaceAsync(profile);

            return ApiResult.Success(new { pluginInstallId = existing.Id }).ToActionResult();
        }
    }
}
