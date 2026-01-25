using EIMSNext.ApiService.ViewModel;
using EIMSNext.Core.Entity;
using EIMSNext.Service.Interface;
using HKH.Mef2.Integration;

namespace EIMSNext.ApiService
{
	public class AuditLogApiService(IResolver resolver) : ApiServiceBase<AuditLog, AuditLogViewModel,IAuditLogService>(resolver)
	{
	}
}
