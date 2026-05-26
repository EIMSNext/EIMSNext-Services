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
using EIMSNext.Core;
using EIMSNext.Core.Repositories;
using EIMSNext.Plugin.Contracts;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Host.Requests;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Host.Authorization;
using HKH.Mef2.Integration;
using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.Service.Host.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param> 
    [ApiVersion(1.0)]
    public class SystemController(IResolver resolver, IPluginRuntimeManager pluginRuntimeManager, IRepository<PluginInstall> pluginInstallRepository) : MefControllerBase(resolver)
    {
        private IPluginRuntimeManager PluginRuntimeManager { get; } = pluginRuntimeManager;
        private IRepository<PluginInstall> PluginInstallRepository { get; } = pluginInstallRepository;

        /// <summary>
        /// 获取当前用户信息
        /// </summary>
        /// <returns></returns>
        [HttpGet("CurrentUser")]
        public IActionResult CurrentUser()
        {
            var user = IdentityContext.CurrentUser!;
            var emp = IdentityContext.CurrentEmployee as Employee;

            return ApiResult.Success(new
            {
                userId = user.Id,
                userName = user.Name,
                phone = user.Phone,
                email = user.Email,
                empId = emp?.Id,
                empCode = emp?.Code,
                empName = emp?.EmpName,
                corpId = IdentityContext.CurrentCorpId,
                deptId = emp?.DepartmentId,
                userType = IdentityContext.IdentityType,
                roles = emp?.Roles.Select(x => x.RoleId)
            }).ToActionResult();
        }

        [HttpGet("AppMenuPerms")]
        public IActionResult GetAppMenuPerms(string appId)
        {
            if (IdentityType.App_Admins.HasFlag(IdentityContext.IdentityType))  //此种类型不应该请求进来
            {
                return Ok(Array.Empty<object>());
            }
            else if (IdentityType.Employee_Admins.HasFlag(IdentityContext.IdentityType))
            {
                var emp = (IdentityContext.CurrentEmployee as Employee)!;

                //TODO: 性能不一定好，先这样写
                var empId = emp.Id;
                var roleIds = emp.Roles.Select(x => x.RoleId).ToList();
                var deptId = emp.DepartmentId;
                var pDeptIds = Resolver.GetService<Department>().Query(x => x.CorpId == IdentityContext.CurrentCorpId && x.HeriarchyId.Contains($"|{deptId}|")).Select(x => x.Id).ToList();
                var formIds = Resolver.GetService<AuthGroup>().Query(x => x.CorpId == IdentityContext.CurrentCorpId && x.AppId == appId && x.Members.Any(m => (m.Type == MemberType.Employee && m.Id == empId) || (m.Type == MemberType.Role && roleIds.Contains(m.Id)) || (m.Type == MemberType.Department && (m.CascadedDept && pDeptIds.Contains(m.Id) || deptId == m.Id)))).Select(x => x.FormId).Distinct().ToList();

                //TODO:仪表盘还没有发布功能，返回所有
                var dashIds = Resolver.GetService<DashboardDef>().Query(x => x.CorpId == IdentityContext.CurrentCorpId && x.AppId == appId).Select(x => x.Id).Distinct().ToList(); ;

                return Ok(formIds.Select(x => new { id = x, type = FormType.Form }).Concat(dashIds.Select(y => new { id = y, type = FormType.Dashboard })));
            }

            return Ok(Array.Empty<object>());
        }

        /// <summary>
        /// 要切换登录的企业ID
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        [HttpPost("SwitchCorp")]
        public async Task<IActionResult> SwitchCorprate(SwitchCorprateRequest req)
        {
            if (string.IsNullOrEmpty(req.CorpId)) return NotFound();

            var user = IdentityContext.CurrentUser! as User;
            user!.Crops.ForEach(x => x.IsDefault = (req.CorpId == x.CorpId));
            await Resolver.GetApiService<User, User>().ReplaceAsync(user);
            return ApiResult.Success(req.CorpId).ToActionResult();
        }

        [HttpPost("JoinCorp")]
        [Permission(Operation = Operation.Write)]
        [IdentityType(IdentityType.NoCorp)]
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

            await Resolver.Resolve<ICorpOnboardingService>().ApplyJoinCorporateAsync(request.CorpId, user);
            return ApiResult.Success().ToActionResult();
        }

        /// <summary>
        /// 更新secret
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        [HttpPost("UpdateSecret")]
        public async Task<IActionResult> UpdateClientSecret(UpdateSecretRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.ClientId)) return NotFound();
            if (string.IsNullOrWhiteSpace(req.Secret)) return BadRequest();

            var clientService = Resolver.GetApiService<Auth.Entities.Client, Auth.Entities.Client>();
            var client = await clientService.GetAsync(req.ClientId);
            if (client != null && client.CorpId == IdentityContext.CurrentCorpId)
            {
                client.ClientSecrets = new List<ClientSecret> { new ClientSecret { Value = req.Secret.Sha256() } };
                await clientService.ReplaceAsync(client);
                return ApiResult.Success(req.ClientId).ToActionResult();
            }

            return NotFound();
        }

        [HttpGet("Plugins")]
        public IActionResult GetPlugins()
        {
            return ApiResult.Success(PluginRuntimeManager.GetPlugins()).ToActionResult();
        }

        [HttpPost("ReloadPlugin")]
        public async Task<IActionResult> ReloadPlugin(CancellationToken cancellationToken)
        {
            var result = await PluginRuntimeManager.ReloadAsync(cancellationToken);
            return ApiResult.Success(result).ToActionResult();
        }

        [HttpGet("PluginInstalls")]
        public IActionResult GetPluginInstalls()
        {
            var corpId = IdentityContext.CurrentCorpId;
            var items = PluginInstallRepository.Queryable
                .Where(x => x.CorpId == corpId && !x.DeleteFlag)
                .OrderByDescending(x => x.InstalledAt)
                .ToList();

            return ApiResult.Success(items).ToActionResult();
        }

        [HttpPost("PluginInstalls/{id}/Enable")]
        public async Task<IActionResult> EnablePluginInstall(string id)
        {
            var entity = PluginInstallRepository.Queryable.FirstOrDefault(x => x.Id == id && x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag);
            if (entity == null)
            {
                return NotFound();
            }

            entity.Enabled = true;
            entity.LastEnabledAt = DateTime.UtcNow.ToTimeStampMs();
            entity.Status = PluginInstallStatus.Installed;
            await PluginInstallRepository.ReplaceAsync(entity);
            return ApiResult.Success(entity.Id).ToActionResult();
        }

        [HttpPost("PluginInstalls/{id}/Disable")]
        public async Task<IActionResult> DisablePluginInstall(string id)
        {
            var entity = PluginInstallRepository.Queryable.FirstOrDefault(x => x.Id == id && x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag);
            if (entity == null)
            {
                return NotFound();
            }

            entity.Enabled = false;
            entity.LastDisabledAt = DateTime.UtcNow.ToTimeStampMs();
            await PluginInstallRepository.ReplaceAsync(entity);
            return ApiResult.Success(entity.Id).ToActionResult();
        }

        [HttpDelete("PluginInstalls/{id}")]
        public async Task<IActionResult> DeletePluginInstall(string id)
        {
            var entity = PluginInstallRepository.Queryable.FirstOrDefault(x => x.Id == id && x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag);
            if (entity == null)
            {
                return NotFound();
            }

            entity.DeleteFlag = true;
            entity.Enabled = false;
            entity.Status = PluginInstallStatus.Uninstalled;
            entity.UninstalledAt = DateTime.UtcNow.ToTimeStampMs();
            await PluginInstallRepository.ReplaceAsync(entity);
            return ApiResult.Success(entity.Id).ToActionResult();
        }
    }
}
