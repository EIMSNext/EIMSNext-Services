using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Host.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.Service.Host.Controllers
{
    [ApiVersion(1.0)]
    public class AppDefController(IResolver resolver) : ApiControllerBase<AppDefApiService, AppDef, AppDefViewModel>(resolver)
    {
        [HttpPost("CreateGroup")]
        public async Task<ActionResult<AppDef>> CreateGroup([FromBody] CreateAppGroupRequest request)
        {
            return Ok(await ApiService.CreateGroup(request));
        }

        [HttpPost("EditGroup")]
        public async Task<ActionResult<AppDef>> EditGroup([FromBody] EditAppGroupRequest request)
        {
            return Ok(await ApiService.EditGroup(request));
        }

        [HttpPost("EditMenu")]
        public async Task<ActionResult<AppDef>> EditMenu([FromBody] EditAppMenuRequest request)
        {
            return Ok(await ApiService.EditMenu(request));
        }

        [HttpPost("DeleteGroup")]
        public async Task<ActionResult<AppDef>> DeleteGroup([FromBody] DeleteAppGroupRequest request)
        {
            return Ok(await ApiService.DeleteGroup(request));
        }

        [HttpPost("SaveMenus")]
        public async Task<ActionResult<AppDef>> SaveMenus([FromBody] SaveAppMenusRequest request)
        {
            return Ok(await ApiService.SaveMenus(request));
        }

        /// <summary>
        /// 将已存在的 <see cref="AppDef"/> 升级为可被应用商店浏览/安装的 <see cref="AppProfile"/>。
        /// 仅 <c>PlatAdmin</c> 可调用。
        /// </summary>
        /// <param name="id">应用定义 Id。</param>
        /// <returns>新创建或已更新的 <see cref="AppProfile"/> Id。</returns>
        [HttpPost("{id}/publish")]
        [IdentityType(IdentityTypeDefaults.PlatAdmin)]
        public async Task<ActionResult<string>> Publish([FromRoute] string id)
        {
            var apiService = Resolver.Resolve<AppPublishApiService>();
            return Ok(await apiService.PublishAsync(id));
        }
    }
}

