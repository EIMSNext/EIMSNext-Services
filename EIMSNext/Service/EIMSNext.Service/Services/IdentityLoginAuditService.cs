using EIMSNext.Entities;
using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;
using HKH.Mef2.Integration;

namespace EIMSNext.Service
{
	public class IdentityLoginAuditService(IResolver resolver) : EntityServiceBase<IdentityLoginAudit>(resolver), IIdentityLoginAuditService
	{
	}
}
