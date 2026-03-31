using Asp.Versioning;
using EIMSNext.ApiService;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Core.Entities;
using EIMSNext.Service.Api.OData;
using HKH.Mef2.Integration;

namespace EIMSNext.Service.Api.Controllers.OData
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
	public class AuditLogController(IResolver resolver) : ReadOnlyODataController<AuditLogApiService, AuditLog, AuditLogViewModel>(resolver)
	{		
	}
}
