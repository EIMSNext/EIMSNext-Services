using Asp.Versioning;
using EIMSNext.ApiService;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Auth.Entities;
using HKH.Mef2.Integration;

namespace EIMSNext.Service.Host.Controllers
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
