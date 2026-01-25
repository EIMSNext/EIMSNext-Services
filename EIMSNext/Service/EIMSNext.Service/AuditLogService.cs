using EIMSNext.Core.Entity;
using EIMSNext.Core.Service;
using EIMSNext.Service.Interface;
using HKH.Mef2.Integration;

namespace EIMSNext.Service
{
	public class AuditLogService(IResolver resolver) : EntityServiceBase<AuditLog>(resolver), IAuditLogService
	{
	}
}
