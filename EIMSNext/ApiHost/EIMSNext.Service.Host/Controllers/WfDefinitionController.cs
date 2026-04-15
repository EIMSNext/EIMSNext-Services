using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.ApiClient.Flow;
using EIMSNext.ApiService;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.Service.Host.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
	public class WfDefinitionController(IResolver resolver) : ApiControllerBase<WfDefinitionApiService, Wf_Definition, WfDefinitionViewModel>(resolver)
	{
	    [HttpPost("CreateVersion")]
	    public async Task<IActionResult> CreateVersion([FromBody] WfDefinitionVersionActionRequest request)
	    {
	        var result = await Resolver.Resolve<IWfDefinitionService>().CreateVersionAsync(request.Id);
	        var flowClient = Resolver.Resolve<FlowApiClient>();
	        await flowClient.Load(new LoadDefRequest { WfDefinitionId = result.ExternalId, Version = result.Version }, IdentityContext.AccessToken);
	        return Ok(result);
	    }

	    [HttpPost("Activate")]
	    public async Task<IActionResult> Activate([FromBody] WfDefinitionVersionActionRequest request)
	    {
	        var result = await Resolver.Resolve<IWfDefinitionService>().ActivateAsync(request.Id);
	        var flowClient = Resolver.Resolve<FlowApiClient>();
	        await flowClient.Load(new LoadDefRequest { WfDefinitionId = result.ExternalId, Version = result.Version }, IdentityContext.AccessToken);
	        return Ok(result);
	    }
	}

    public class WfDefinitionVersionActionRequest
    {
        public string Id { get; set; } = string.Empty;
    }
}
