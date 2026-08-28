using Asp.Versioning;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Common;
using EIMSNext.Service.Host.Authorization;
using EIMSNext.Entities;
using HKH.Mef2.Integration;
using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.Service.Host.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
	public class IdentityLoginAuditController(IResolver resolver) : ApiControllerBase<IdentityLoginAuditApiService, IdentityLoginAudit, IdentityLoginAuditViewModel>(resolver)
	{
		[HttpPost("Export")]
		[Permission(Operation = Operation.Read)]
		public async Task<ActionResult> Export([FromBody] IdentityLoginAuditExportRequest request)
		{
			return Ok(await ApiService.ExportAsync(request));
		}
	}
}
