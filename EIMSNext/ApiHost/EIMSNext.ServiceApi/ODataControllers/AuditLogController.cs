using Asp.Versioning;
using EIMSNext.ApiService;
using EIMSNext.ApiService.ViewModel;
using EIMSNext.Core.Entity;
using EIMSNext.ServiceApi.OData;
using HKH.Mef2.Integration;

namespace EIMSNext.ServiceApi.ODataControllers
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
