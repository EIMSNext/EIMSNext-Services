using EIMSNext.Auth.Entity;
using EIMSNext.Core.Service;
using EIMSNext.Service.Interface;
using HKH.Mef2.Integration;

namespace EIMSNext.Service
{
	public class AuditLoginService(IResolver resolver) : EntityServiceBase<AuditLogin>(resolver), IAuditLoginService
	{
	}
}
