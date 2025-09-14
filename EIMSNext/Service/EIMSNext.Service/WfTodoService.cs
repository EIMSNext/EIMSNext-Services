using HKH.Mef2.Integration;

using EIMSNext.Core.Service;
using EIMSNext.Entity;
using EIMSNext.Service.Interface;

namespace EIMSNext.Service
{
    public class WfTodoService(IResolver resolver) : EntityServiceBase<Wf_Todo>(resolver), IWfTodoService
	{
    }
}
