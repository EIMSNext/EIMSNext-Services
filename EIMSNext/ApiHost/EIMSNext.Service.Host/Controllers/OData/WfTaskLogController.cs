using Asp.Versioning;
using EIMSNext.ApiService;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Entities;
using EIMSNext.Service.Host.OData;
using HKH.Mef2.Integration;
using Microsoft.AspNetCore.OData.Query;

namespace EIMSNextt.API.ODataControllers
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
    public class WfTaskLogController(IResolver resolver) : ReadOnlyODataController<WfTaskLogApiService, Wf_TaskLog, WfTaskLogViewModel>(resolver)
    {
        protected override IQueryable<WfTaskLogViewModel> FilterByPermission(IQueryable<WfTaskLogViewModel> query, ODataQueryOptions<WfTaskLogViewModel> options)
        {
            if (IdentityContext.CurrentEmployee != null)
            {
                var empId = IdentityContext.CurrentEmployee.Id;
                return query.Where(x => x.Approver != null && x.Approver.Id == empId);
            }

            return query;
        }
    }
}
