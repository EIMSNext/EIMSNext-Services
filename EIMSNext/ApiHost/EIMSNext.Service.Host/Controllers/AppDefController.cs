using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Entities;
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
    }
}
