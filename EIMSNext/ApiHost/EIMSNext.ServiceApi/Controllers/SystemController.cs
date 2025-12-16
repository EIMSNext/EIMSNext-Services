using Asp.Versioning;
using EIMSNext.ApiService.Extension;
using EIMSNext.ApiService.ViewModel;
using EIMSNext.Auth.Entity;
using EIMSNext.Common;
using EIMSNext.Entity;
using EIMSNext.ServiceApi.Authorization;
using EIMSNext.ServiceApi.Extension;
using EIMSNext.ServiceApi.Request;
using HKH.Mef2.Integration;
using IdentityServer4.Models;
using Microsoft.AspNetCore.Mvc;


namespace EIMSNext.ServiceApi.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiController, ApiVersion(1.0)]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class SystemController(IResolver resolver) : MefControllerBase(resolver)
    {
        /// <summary>
        /// 获取当前用户信息
        /// </summary>
        /// <returns></returns>
        [HttpGet("currentuser") ]
        public IActionResult CurrentUser()
        {
            var appService = Resolver.GetApiService<App, AppViewModel>();
            var user = IdentityContext.CurrentUser!;
            var emp = IdentityContext.CurrentEmployee;
            var apps = appService.All().Select(x => new
            {
                x.Id,
                x.Name,
                x.Description,
                x.Icon,
                x.SortIndex
            });

            return ApiResult.Success(new
            {
                userId = user.Id,
                userName = user.Name,
                empId = emp?.Id,
                empCode = emp?.Code,
                empName = emp?.EmpName,
                corpId = IdentityContext.CurrentCorpId,
                appId = IdentityContext.CurrentAppId,
                userType = IdentityContext.IdentityType,
                apps
            }).ToActionResult();
        }

        /// <summary>
        /// 要切换登录的企业ID
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        [HttpPost("switchcorp")]
        public async Task<IActionResult> SwitchCorprate(SwitchCorprateRequest req)
        {
            if (string.IsNullOrEmpty(req.CorpId)) return NotFound();

            var user = IdentityContext.CurrentUser!.AsUser();
            user.Crops.ForEach(x => x.IsDefault = (req.CorpId == x.CorpId));
            await Resolver.GetApiService<User, User>().ReplaceAsync(user);
            return ApiResult.Success(req.CorpId).ToActionResult();
        }

        /// <summary>
        /// 更新secret
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        [HttpPost("updatesecret")]
        public async Task<IActionResult> UpdateClientSecret(UpdateSecretRequest req)
        {
            if (string.IsNullOrEmpty(req.ClientId)) return NotFound();

            var clientService = Resolver.GetApiService<Auth.Entity.Client, Auth.Entity.Client>();
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
