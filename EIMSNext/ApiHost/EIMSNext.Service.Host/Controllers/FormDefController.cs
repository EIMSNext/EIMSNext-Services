using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.ApiService;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Entities;
using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.Service.Host.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
	public class FormDefController(IResolver resolver) : ApiControllerBase<FormDefApiService, FormDef, FormDefViewModel>(resolver)
	{
        [HttpGet("GetFormsIncludeCross")]
        public IActionResult GetFormsIncludeCross([FromQuery] string appId)
        {
            return Ok(ApiService.GetFormsIncludeCross(appId));
        }
	}
}
