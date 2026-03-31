using EIMSNext.Core.Entities;
using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;
using HKH.Mef2.Integration;

namespace EIMSNext.Service
{
	public class AuditLogService(IResolver resolver) : EntityServiceBase<AuditLog>(resolver), IAuditLogService
	{
	}
}
