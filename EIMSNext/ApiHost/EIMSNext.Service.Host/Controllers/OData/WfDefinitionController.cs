using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.Service.Host.OData;
using EIMSNext.Service.Host.Requests;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Formatter;

namespace EIMSNext.Service.Host.Controllers.OData
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
	public class WfDefinitionController(IResolver resolver) : ODataController<WfDefinitionApiService, Wf_Definition, WfDefinitionViewModel, WfDefinitionRequest>(resolver)
	{
	    public override async Task<ActionResult> Delete([FromODataUri] string key, [FromBody] DeleteBatch? batch)
	    {
	        try
	        {
	            return await base.Delete(key, batch);
	        }
	        catch (Exception ex)
	        {
	            return BadRequest(ex.Message);
	        }
	    }
	}
}
