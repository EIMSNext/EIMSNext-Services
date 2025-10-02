using Asp.Versioning;
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
    public class WfApprovalLogController(IResolver resolver) : ReadOnlyODataController<Wf_ApprovalLog, WfApprovalLogViewModel>(resolver)
    {

    }
}
