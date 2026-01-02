using Asp.Versioning;
using EIMSNext.ApiService;
using EIMSNext.ApiService.ViewModel;
using EIMSNext.Entity;
using EIMSNext.ServiceApi.OData;
using HKH.Mef2.Integration;

namespace EIMSNextt.API.ODataControllers
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
    public class WfApprovalLogController(IResolver resolver) : ReadOnlyODataController<WfApprovalLogApiService, Wf_ApprovalLog, WfApprovalLogViewModel>(resolver)
    {
        protected override IQueryable<WfApprovalLogViewModel> FilterByPermission(IQueryable<WfApprovalLogViewModel> query)
        {
            if (IdentityContext.CurrentEmployee != null)
            {
                var empId = IdentityContext.CurrentEmployee.Id;
                return query.Where(x => x.Approver != null && x.Approver.EmpId == empId);
            }

            return query;
        }
    }
}
