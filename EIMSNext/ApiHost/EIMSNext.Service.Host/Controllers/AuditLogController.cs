using Asp.Versioning;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Common;
using EIMSNext.Core.Entities;
using EIMSNext.Service.Host.Authorization;
using HKH.Mef2.Integration;
using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.Service.Host.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
	public class AuditLogController(IResolver resolver) : ApiControllerBase<AuditLogApiService, AuditLog, AuditLogViewModel>(resolver)
	{
		[HttpPost("Export")]
		[Permission(Operation = Operation.Read)]
		public async Task<ActionResult> Export([FromBody] AuditLogExportRequest request)
		{
			return Ok(ApiResult.Success(await ApiService.ExportAsync(request)));
		}
	}
}
