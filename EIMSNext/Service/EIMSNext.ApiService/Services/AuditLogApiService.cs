using EIMSNext.ApiService.ViewModels;
using EIMSNext.Core.Entities;
using EIMSNext.Service.Contracts;
using HKH.Mef2.Integration;

namespace EIMSNext.ApiService
{
	public class AuditLogApiService(IResolver resolver) : ApiServiceBase<AuditLog, AuditLogViewModel,IAuditLogService>(resolver)
	{
	}
}
