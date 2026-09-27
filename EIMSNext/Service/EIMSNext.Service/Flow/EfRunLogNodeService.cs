using HKH.Mef2.Integration;

using EIMSNext.Core.Services;
using EIMSNext.Entities;
using EIMSNext.Service.Contracts;

namespace EIMSNext.Service
{
	public class EfRunLogNodeService(IResolver resolver) : EntityServiceBaseCore<Ef_RunLogNode>(resolver), IEfRunLogNodeService
	{
		protected override bool LogAudit => false;
	}
}
