using Asp.Versioning;
using EIMSNext.ApiHost.Controllers;
using EIMSNext.ApiHost.Extensions;
using EIMSNext.ApiCore.Plugin;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.Extensions;
using EIMSNext.Auth.Entities;
using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Host.Authorization;
using EIMSNext.Service.Host.Requests;
using HKH.Mef2.Integration;
using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.Service.Host.Controllers
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
    [IdentityType(IdentityTypeDefaults.BusinessUser)]
    public class SystemController(IResolver resolver) : MefControllerBase(resolver)
    {
        private static readonly HashSet<string> AvatarFileExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".gif", ".jpeg", ".jpg", ".png", ".webp"
        };

        private IClientApiService ClientApiService => Resolver.Resolve<IClientApiService>();
        private UserApiService UserApiService => Resolver.Resolve<UserApiService>();
        private ICorpOnboardingService CorpOnboardingService => Resolver.Resolve<ICorpOnboardingService>();
        private PluginStoreApiService PluginStoreApiService => Resolver.Resolve<PluginStoreApiService>();
        private ECoinPriceApiService ECoinPriceApiService => Resolver.Resolve<ECoinPriceApiService>();

        /// <summary>
        /// 获取当前用户信息
        /// </summary>
        /// <returns></returns>
        [HttpGet("CurrentUser")]
        [IdentityType(IdentityTypeDefaults.BusinessUser)]
        public IActionResult CurrentUser()
        {
            var user = IdentityContext.CurrentUser;
            var emp = IdentityContext.CurrentEmployee as Employee;
            var departmentIds = emp == null
                ? new List<string>()
                : Resolver.GetRepository<EmployeeDepartment>().Queryable
                    .Where(x => x.CorpId == IdentityContext.CurrentCorpId && x.EmployeeId == emp.Id)
                    .OrderBy(x => x.SortValue)
                    .Select(x => x.DepartmentId)
                    .ToList();

            return ApiResult.Success(new
            {
                userId = user?.Id ?? IdentityContext.CurrentUserID,
                userName = user?.Name ?? User.Identity?.Name ?? IdentityContext.CurrentUserID,
                phone = user?.Phone,
                email = user?.Email,
                avatar = (user as User)?.Avatar,
                empId = emp?.Id,
                empCode = emp?.Code,
                empName = emp?.EmpName,
                corpId = IdentityContext.CurrentCorpId,
                departmentIds,
                userType = IdentityContext.IdentityType,
                roles = emp?.Roles.Select(x => x.RoleId)
            }).ToActionResult();
        }

        [HttpPost("UpdateAvatar")]
        [IdentityType(IdentityTypeDefaults.BusinessUser)]
        public async Task<IActionResult> UpdateAvatar([FromBody] UpdateAvatarRequest request)
        {
            if (IdentityContext.CurrentUser is not User user)
            {
                return Unauthorized();
            }

            var avatar = request.Avatar?.Trim().Replace('\\', '/');
            var extension = Path.GetExtension(avatar ?? string.Empty).ToLowerInvariant();
            var expectedAvatar = $"Avatar/{user.Id}{extension}";
            if (!AvatarFileExtensions.Contains(extension)
                || !string.Equals(avatar, expectedAvatar, StringComparison.Ordinal))
            {
                return BadRequest("头像路径无效");
            }

            user.Avatar = avatar;
            await UserApiService.ReplaceAsync(user);
            return ApiResult.Success(new { avatar }).ToActionResult();
        }

        [HttpGet("AdminPermissions")]
        [IdentityType(IdentityTypeDefaults.AppAdmin)]
        public IActionResult GetAdminPermissions()
        {
            return ApiResult.Success(Resolver.Resolve<AdminPermissionEvaluator>().GetSnapshot()).ToActionResult();
        }

        [HttpGet("AppMenuPerms")]
        public IActionResult GetAppMenuPerms(string appId)
        {
            return Ok(Resolver.Resolve<AdminPermissionEvaluator>().GetAppMenuPermissions(appId));
        }

        /// <summary>
        /// 要切换登录的企业ID
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        [HttpPost("SwitchCorp")]
        public async Task<IActionResult> SwitchCorprate(SwitchCorprateRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.CorpId)) return BadRequest("企业不能为空");

            if (IdentityContext.CurrentUser is not User user)
                return Unauthorized();

            var targetCorp = user.Crops?.FirstOrDefault(x =>
                string.Equals(x.CorpId, req.CorpId.Trim(), StringComparison.Ordinal));
            if (targetCorp == null)
                return Forbid();

            foreach (var corp in user.Crops ?? [])
                corp.IsDefault = string.Equals(corp.CorpId, targetCorp.CorpId, StringComparison.Ordinal);

            await UserApiService.ReplaceAsync(user);
            return ApiResult.Success(targetCorp.CorpId).ToActionResult();
        }

        [HttpPost("JoinCorp")]
        [IdentityType(IdentityTypeDefaults.Authenticated)]
        public async Task<IActionResult> JoinCorp([FromBody] ApplyJoinCorporateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.CorpId))
            {
                return BadRequest("请选择要加入的企业");
            }

            var user = IdentityContext.CurrentUser as User;
            if (user == null)
            {
                return BadRequest("未登录用户");
            }

            await CorpOnboardingService.ApplyJoinCorporateAsync(request.CorpId, user);
            return ApiResult.Success().ToActionResult();
        }

        /// <summary>
        /// 更新secret
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        [HttpPost("UpdateSecret")]
        [IdentityType(IdentityTypeDefaults.CorpAdmin)]
        public async Task<IActionResult> UpdateClientSecret(UpdateSecretRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.ClientId)) return NotFound();
            if (string.IsNullOrWhiteSpace(req.Secret)) return BadRequest();

            var client = await ClientApiService.GetAsync(req.ClientId);
            if (client != null && client.CorpId == IdentityContext.CurrentCorpId)
            {
                client.ClientSecrets = new List<ClientSecret> { new ClientSecret { Value = req.Secret.Sha256() } };
                await ClientApiService.ReplaceAsync(client);
                return ApiResult.Success(req.ClientId).ToActionResult();
            }

            return NotFound();
        }

        [HttpGet("Plugins")]
        [IdentityType(IdentityTypeDefaults.Authenticated)]
        public IActionResult GetPlugins()
        {
            return ApiResult.Success(PluginStoreApiService.GetInstalledRuntimePlugins()).ToActionResult();
        }

        [HttpGet("EnabledPlugins")]
        public IActionResult GetEnabledPlugins()
        {
            return ApiResult.Success(PluginStoreApiService.GetEnabledPlugins()).ToActionResult();
        }

        [HttpPost("ReloadPlugin")]
        [IdentityType(IdentityTypeDefaults.PlatAdmin)]
        public async Task<IActionResult> ReloadPlugin(CancellationToken cancellationToken)
        {
            var pluginRuntimeManager = Resolver.Resolve<IPluginRuntimeManager>();
            var result = await pluginRuntimeManager.ReloadAsync(cancellationToken);
            return ApiResult.Success(result).ToActionResult();
        }

        [HttpGet("PluginInstalls")]
        [IdentityType(IdentityTypeDefaults.AppAdmin)]
        public IActionResult GetPluginInstalls()
        {
            return ApiResult.Success(PluginStoreApiService.GetPluginInstalls()).ToActionResult();
        }

        [HttpPost("PluginInstalls/{id}/Enable")]
        [IdentityType(IdentityTypeDefaults.CorpAdmin)]
        public async Task<IActionResult> EnablePluginInstall(string id)
        {
            var entity = await PluginStoreApiService.EnablePluginInstallAsync(id);
            if (entity == null)
            {
                return NotFound();
            }
            return ApiResult.Success(entity.Id).ToActionResult();
        }

        [HttpPost("PluginInstalls/{id}/Disable")]
        [IdentityType(IdentityTypeDefaults.CorpAdmin)]
        public async Task<IActionResult> DisablePluginInstall(string id)
        {
            var entity = await PluginStoreApiService.DisablePluginInstallAsync(id);
            if (entity == null)
            {
                return NotFound();
            }
            return ApiResult.Success(entity.Id).ToActionResult();
        }

        [HttpDelete("PluginInstalls/{id}")]
        [IdentityType(IdentityTypeDefaults.CorpAdmin)]
        public async Task<IActionResult> DeletePluginInstall(string id)
        {
            var entity = await PluginStoreApiService.DeletePluginInstallAsync(id);
            if (entity == null)
            {
                return NotFound();
            }
            return ApiResult.Success(entity.Id).ToActionResult();
        }

        [HttpGet("pluginstore")]
        public IActionResult GetPluginStore([FromQuery] PluginProfileQueryRequest request)
        {
            var (total, items) = PluginStoreApiService.GetPluginStore(request);
            return ApiResult.Success(new { total, items }).ToActionResult();
        }

        [HttpGet("pluginstore/{id}")]
        public IActionResult GetPluginStoreDetail(string id)
        {
            var detail = PluginStoreApiService.GetPluginStoreDetail(id);
            if (detail == null)
            {
                return NotFound();
            }
            return ApiResult.Success(detail).ToActionResult();
        }

        [HttpPost("pluginstore/{id}/install")]
        [IdentityType(IdentityTypeDefaults.CorpAdmin)]
        public async Task<IActionResult> InstallPlugin(string id)
        {
            var result = await PluginStoreApiService.InstallPluginAsync(id);
            if (result == null)
            {
                return Unauthorized();
            }
            return ApiResult.Success(new { pluginInstallId = result.PluginInstallId }).ToActionResult();
        }

        [HttpPost("pluginstore/publish")]
        [IdentityType(IdentityTypeDefaults.PlatAdmin)]
        public async Task<IActionResult> PublishPlugin([FromBody] PluginPublishRequest request)
        {
            var profile = await PluginStoreApiService.PublishAsync(request);
            return ApiResult.Success(new { profile.Id, profile.PluginId, profile.Version }).ToActionResult();
        }

        [HttpPost("ecoinprice/batch")]
        [IdentityType(IdentityTypeDefaults.PlatAdmin)]
        public async Task<IActionResult> BatchUpsertECoinPrices([FromBody] List<ECoinPriceBatchItemRequest> requests)
        {
            var result = await ECoinPriceApiService.BatchUpsertAsync(requests);
            return ApiResult.Success(result).ToActionResult();
        }
    }
}
