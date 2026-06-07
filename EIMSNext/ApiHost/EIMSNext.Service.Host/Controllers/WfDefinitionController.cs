using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.ApiClient.Flow;
using EIMSNext.ApiHost.Extensions;
using EIMSNext.ApiService;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Common;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.Service.Host.Controllers
{
    /// <summary>
    /// 工作流/数据流定义控制器。
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

	    /// <summary>
	    /// 获取数据流最近一次HTTP触发样例。
	    /// </summary>
	    [HttpGet("HttpSample")]
	    public async Task<IActionResult> GetHttpSampleAsync([FromQuery] string dataflowId, [FromQuery] string corpId)
	    {
	        if (string.IsNullOrWhiteSpace(dataflowId) || string.IsNullOrWhiteSpace(corpId))
	        {
	            return BadRequest("dataflowId和corpId不能为空");
	        }

	        var def = Resolver.Resolve<IWfDefinitionService>().Get(dataflowId);
	        if (def == null || !string.Equals(def.CorpId, corpId, StringComparison.Ordinal))
	        {
	            return NotFound("智能助手不存在");
	        }

	        var hookApi = Resolver.Resolve<DataflowHookApiService>();
	        var sample = await hookApi.GetLatestSampleAsync(corpId, dataflowId);
	        if (sample == null)
	        {
	            return ApiResult.Success(new { hasSample = false }).ToActionResult();
	        }

	        var triggerSetting = def.Metadata.Steps.FirstOrDefault()?.DfNodeSetting?.TriggerSetting;
	        var capturedAt = triggerSetting?.HttpTrigger?.SampleCapturedAt ?? sample.CapturedAt;

	        return ApiResult.Success(new
	        {
	            hasSample = true,
	            capturedAt,
	            sampleFields = triggerSetting?.HttpTrigger?.SampleFields ?? [],
	        }).ToActionResult();
	    }
	}

    public class WfDefinitionVersionActionRequest
    {
        public string Id { get; set; } = string.Empty;
    }
}
