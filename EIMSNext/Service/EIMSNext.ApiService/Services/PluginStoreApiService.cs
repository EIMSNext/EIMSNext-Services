using EIMSNext.ApiCore.Plugin;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.Common.Extensions;
using EIMSNext.Core;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Plugin.Contracts;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;

namespace EIMSNext.ApiService
{
    /// <summary>
    /// 插件商店与安装管理服务。
    /// 负责已安装插件查询/启停/卸载，以及插件商店列表/详情/安装业务逻辑。
    /// </summary>
    public class PluginStoreApiService : ApiServiceBase
    {
        private const string PublishedStatus = "Published";

        public PluginStoreApiService(IResolver resolver) : base(resolver)
        {
        }

        private IPluginRuntimeManager PluginRuntimeManager => Resolver.Resolve<IPluginRuntimeManager>();
        private IRepository<PluginInstall> PluginInstallRepository => Resolver.GetRepository<PluginInstall>();
        private IRepository<PluginProfile> PluginProfileRepository => Resolver.GetRepository<PluginProfile>();

        public IEnumerable<PluginRuntimeInfo> GetEnabledPlugins()
        {
            var corpId = IdentityContext.CurrentCorpId;
            if (string.IsNullOrWhiteSpace(corpId))
            {
                return Array.Empty<PluginRuntimeInfo>();
            }

            var now = DateTime.UtcNow.ToTimeStampMs();
            var enabledInstalls = PluginInstallRepository.Queryable
                .Where(x => x.CorpId == corpId)
                .ToList()
                .Where(x => IsInstallUsable(x, now))
                .ToList();

            return enabledInstalls
                .Select(x => PluginRuntimeManager.GetPlugin(x.PluginId))
                .Where(x => x != null)
                .Cast<PluginRuntimeInfo>()
                .ToList();
        }

        public IEnumerable<PluginInstall> GetPluginInstalls()
        {
            var corpId = IdentityContext.CurrentCorpId;
            return PluginInstallRepository.Queryable
                .Where(x => x.CorpId == corpId && !x.DeleteFlag)
                .OrderByDescending(x => x.InstalledAt)
                .ToList();
        }

        public async Task<PluginInstall?> EnablePluginInstallAsync(string id)
        {
            var entity = PluginInstallRepository.Queryable
                .FirstOrDefault(x => x.Id == id && x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag);
            if (entity == null)
            {
                return null;
            }

            var now = DateTime.UtcNow.ToTimeStampMs();
            if (entity.ExpireAt != null && entity.ExpireAt <= now)
            {
                return null;
            }

            entity.Enabled = true;
            entity.LastEnabledAt = now;
            entity.Status = PluginInstallStatus.Installed;
            await PluginInstallRepository.ReplaceAsync(entity);
            return entity;
        }

        public async Task<PluginInstall?> DisablePluginInstallAsync(string id)
        {
            var entity = PluginInstallRepository.Queryable
                .FirstOrDefault(x => x.Id == id && x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag);
            if (entity == null)
            {
                return null;
            }

            entity.Enabled = false;
            entity.LastDisabledAt = DateTime.UtcNow.ToTimeStampMs();
            await PluginInstallRepository.ReplaceAsync(entity);
            return entity;
        }

        public async Task<PluginInstall?> DeletePluginInstallAsync(string id)
        {
            var entity = PluginInstallRepository.Queryable
                .FirstOrDefault(x => x.Id == id && x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag);
            if (entity == null)
            {
                return null;
            }

            entity.DeleteFlag = true;
            entity.Enabled = false;
            entity.Status = PluginInstallStatus.Uninstalled;
            entity.UninstalledAt = DateTime.UtcNow.ToTimeStampMs();
            await PluginInstallRepository.ReplaceAsync(entity);

            await UpdatePluginInstallCountAsync(entity.PluginId, -1);

            return entity;
        }

        public (long total, IReadOnlyList<object> items) GetPluginStore(PluginProfileQueryRequest request)
        {
            var query = PluginProfileRepository.Queryable.Where(x => !x.DeleteFlag && x.Status == PublishedStatus);

            if (!string.IsNullOrWhiteSpace(request.Keyword))
            {
                query = query.Where(x => x.Name.Contains(request.Keyword) || x.Summary.Contains(request.Keyword) || x.Tags.Contains(request.Keyword));
            }

            if (!string.IsNullOrWhiteSpace(request.Category))
            {
                query = query.Where(x => x.Category == request.Category);
            }

            if (!string.IsNullOrWhiteSpace(request.Scenario))
            {
                query = query.Where(x => x.Scenario == request.Scenario);
            }

            if (request.Recommended == true)
            {
                query = query.Where(x => x.IsRecommended);
            }

            var corpId = IdentityContext.CurrentCorpId;
            var installedPluginIds = string.IsNullOrWhiteSpace(corpId)
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : PluginInstallRepository.Queryable
                    .Where(x => x.CorpId == corpId && !x.DeleteFlag && x.Status == PluginInstallStatus.Installed)
                    .Select(x => x.PluginId)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var total = query.Count();
            var items = query
                .OrderByDescending(x => x.IsRecommended)
                .ThenByDescending(x => x.SortIndex)
                .Skip(request.Skip)
                .Take(request.Take)
                .ToList()
                .Select(x => (object)new
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

            return (total, items);
        }

        public object? GetPluginStoreDetail(string id)
        {
            var profile = PluginProfileRepository.Get(id);
            if (profile == null || profile.DeleteFlag || profile.Status != PublishedStatus)
            {
                return null;
            }

            var corpId = IdentityContext.CurrentCorpId;
            var install = string.IsNullOrWhiteSpace(corpId)
                ? null
                : PluginInstallRepository.Queryable.FirstOrDefault(x => x.CorpId == corpId && x.PluginId == profile.PluginId && !x.DeleteFlag && x.Status == PluginInstallStatus.Installed);
            var now = DateTime.UtcNow.ToTimeStampMs();
            var runtime = PluginRuntimeManager.GetPlugin(profile.PluginId);
            var functions = runtime?.Functions.Select(x => (object)new
                {
                    x.Id,
                    x.Name,
                    Description = x.Description ?? string.Empty,
                    inputFields = x.InputFields.ToList(),
                    resultFields = x.ResultFields.ToList(),
                }).ToList()
                ?? profile.Functions.Select(x => (object)new
                {
                    x.Id,
                    x.Name,
                    Description = x.Description ?? string.Empty,
                    inputFields = x.InputFields.ToList(),
                    resultFields = x.ResultFields.ToList(),
                }).ToList();

            return new
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
                installEnabled = install != null && IsInstallUsable(install, now),
            };
        }

        public async Task<PluginInstallResult?> InstallPluginAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(IdentityContext.CurrentUserID) || string.IsNullOrWhiteSpace(IdentityContext.CurrentCorpId))
            {
                return null;
            }

            var profile = PluginProfileRepository.Get(id);
            if (profile == null || profile.DeleteFlag || profile.Status != PublishedStatus)
            {
                return null;
            }

            var corpId = IdentityContext.CurrentCorpId;
            var now = DateTime.UtcNow.ToTimeStampMs();
            var existing = PluginInstallRepository.Queryable
                .FirstOrDefault(x => x.CorpId == corpId && x.PluginId == profile.PluginId && !x.DeleteFlag);

            if (existing != null)
            {
                return new PluginInstallResult { PluginInstallId = existing.Id };
            }

            var entity = new PluginInstall
            {
                Id = PluginInstallRepository.NewId(),
                CorpId = corpId,
                PluginId = profile.PluginId,
                Name = profile.Name,
                Summary = profile.Summary,
                Icon = profile.Icon,
                Status = PluginInstallStatus.Installed,
                Enabled = true,
                InstalledAt = now,
                InstalledBy = IdentityContext.CurrentEmployee?.ToOperator() ?? new Operator(IdentityContext.CurrentUserID, IdentityContext.CurrentUser?.Name ?? string.Empty, IdentityContext.CurrentUser?.Name ?? string.Empty),
                Source = "pluginstore"
            };
            await PluginInstallRepository.InsertAsync(entity);

            await UpdatePluginInstallCountAsync(profile.PluginId, 1);

            return new PluginInstallResult { PluginInstallId = entity.Id };
        }

        public IEnumerable<PluginRuntimeInfo> GetInstalledRuntimePlugins() => PluginRuntimeManager.GetPlugins();

        private async Task UpdatePluginInstallCountAsync(string pluginId, long delta)
        {
            var profiles = PluginProfileRepository.Queryable
                .Where(x => x.PluginId == pluginId && !x.DeleteFlag)
                .ToList();

            foreach (var profile in profiles)
            {
                profile.InstallCount = Math.Max(0, profile.InstallCount + delta);
                await PluginProfileRepository.ReplaceAsync(profile);
            }
        }

        private static bool IsInstallUsable(PluginInstall install, long now)
        {
            return !install.DeleteFlag
                && install.Status == PluginInstallStatus.Installed
                && install.Enabled
                && (install.ExpireAt == null || install.ExpireAt > now);
        }

        public class PluginInstallResult
        {
            public string PluginInstallId { get; set; } = string.Empty;
        }
    }
}
