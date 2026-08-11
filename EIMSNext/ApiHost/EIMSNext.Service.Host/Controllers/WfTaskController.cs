using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.ApiService;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Query;
using EIMSNext.Service.Entities;

namespace EIMSNext.Service.Host.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
	public class WfTaskController(IResolver resolver) : ApiControllerBase<WfTaskApiService, Wf_Task, WfTaskViewModel>(resolver)
	{
		protected override DynamicFindOptions<Wf_Task> FilterResult(DynamicFindOptions<Wf_Task> query)
		{
			return FilterByPermission(FilterByCorpId(query));
		}
	}
}
