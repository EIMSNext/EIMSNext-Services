using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.ApiService;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Common;
using EIMSNext.Entities;
using EIMSNext.Service.Host.Authorization;
using EIMSNext.Service.Host.Requests;
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

        [Permission(Operation = Operation.Edit)]
        [HttpDelete("{formId}/field")]
        public async Task<IActionResult> PurgeFieldChangeLogs(
            [FromRoute] string formId,
            [FromBody] FieldChangeLogDeleteRequest request)
        {
            await ApiService.PurgeFieldChangeLogsAsync(formId, request.FieldIds, request.ClearAll);
            return NoContent();
        }
	}
}
