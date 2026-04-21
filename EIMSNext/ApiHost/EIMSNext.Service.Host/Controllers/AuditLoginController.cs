using Asp.Versioning;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Common;
using EIMSNext.Service.Host.Authorization;
using EIMSNext.Auth.Entities;
using HKH.Mef2.Integration;
using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.Service.Host.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
	public class AuditLoginController(IResolver resolver) : ApiControllerBase<AuditLoginApiService, AuditLogin, AuditLoginViewModel>(resolver)
	{
		[HttpPost("Export")]
		[Permission(Operation = Operation.Read)]
		public async Task<ActionResult> Export([FromBody] AuditLoginExportRequest request)
		{
			return Ok(ApiResult.Success(await ApiService.ExportAsync(request)));
		}
	}
}
