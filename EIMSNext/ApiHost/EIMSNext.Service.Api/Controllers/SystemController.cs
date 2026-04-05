using Asp.Versioning;
using EIMSNext.ApiHost.Controllers;
using EIMSNext.ApiHost.Extensions;
using EIMSNext.ApiService;
using EIMSNext.ApiService.Extensions;
using EIMSNext.Auth.Entities;
using EIMSNext.Common;
using EIMSNext.Core;
using EIMSNext.Service.Api.Requests;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;
using IdentityServer4.Models;
using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.Service.Api.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param> 
    [ApiVersion(1.0)]
    public class SystemController(IResolver resolver) : MefControllerBase(resolver)
    {
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
                Ok(Array.Empty<object>());
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

        /// <summary>
        /// 更新secret
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        [HttpPost("UpdateSecret")]
        public async Task<IActionResult> UpdateClientSecret(UpdateSecretRequest req)
        {
            if (string.IsNullOrEmpty(req.ClientId)) return NotFound();

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
    }
}
