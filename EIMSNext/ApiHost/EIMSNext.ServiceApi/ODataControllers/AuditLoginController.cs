using Asp.Versioning;

using HKH.Mef2.Integration;

using EIMSNext.ServiceApi.OData;
using EIMSNext.ApiService.ViewModel;
using EIMSNext.Entity;

namespace EIMSNext.ServiceApi.ODataControllers
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
    public class AuditLoginController(IResolver resolver) : ReadOnlyODataController<AuditLogin, AuditLoginViewModel>(resolver)
    {

    }
}
