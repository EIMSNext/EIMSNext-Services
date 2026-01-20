using Asp.Versioning;
using EIMSNext.ApiService;
using EIMSNext.ApiService.ViewModel;
using EIMSNext.Auth.Entity;
using HKH.Mef2.Integration;

namespace EIMSNext.ServiceApi.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
	public class AuditLoginController(IResolver resolver) : ApiControllerBase<AuditLoginApiService, AuditLogin, AuditLoginViewModel>(resolver)
	{
		
	}
}
