using EIMSNext.ApiService.ViewModels;
using EIMSNext.Auth.Entities;
using EIMSNext.Service.Contracts;
using HKH.Mef2.Integration;

namespace EIMSNext.ApiService
{
    public class AuditLoginApiService(IResolver resolver) : ApiServiceBase<AuditLogin, AuditLoginViewModel, IAuditLoginService>(resolver)
    {
    }
}
