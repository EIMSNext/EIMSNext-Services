using EIMSNext.Auth.Entities;
using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;
using HKH.Mef2.Integration;

namespace EIMSNext.Service
{
	public class AuditLoginService(IResolver resolver) : EntityServiceBase<AuditLogin>(resolver), IAuditLoginService
	{
	}
}
