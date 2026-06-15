using HKH.Mef2.Integration;

using EIMSNext.Core.Services;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Contracts;

namespace EIMSNext.Service
{
	public class DfRunLogNodeService(IResolver resolver) : MongoEntityServiceBase<Df_RunLogNode>(resolver), IDfRunLogNodeService
	{
		protected override bool LogAudit => false;
	}
}
