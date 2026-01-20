using Asp.Versioning;
using EIMSNext.ApiService;
using EIMSNext.ApiService.ViewModel;
using EIMSNext.Core.Entity;
using HKH.Mef2.Integration;

namespace EIMSNext.ServiceApi.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
	public class AuditLogController(IResolver resolver) : ApiControllerBase<AuditLogApiService, AuditLog, AuditLogViewModel>(resolver)
	{
		
	}
}
