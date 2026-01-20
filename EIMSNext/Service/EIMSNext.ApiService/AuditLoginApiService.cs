using EIMSNext.ApiService.ViewModel;
using EIMSNext.Auth.Entity;
using EIMSNext.Service.Interface;
using HKH.Mef2.Integration;

namespace EIMSNext.ApiService
{
    public class AuditLoginApiService(IResolver resolver) : ApiServiceBase<AuditLogin, AuditLoginViewModel, IAuditLoginService>(resolver)
    {
    }
}
