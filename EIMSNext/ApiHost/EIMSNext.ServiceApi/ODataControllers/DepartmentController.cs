using Asp.Versioning;
using EIMSNext.ApiService.RequestModel;
using EIMSNext.ApiService.ViewModel;
using EIMSNext.Entity;
using EIMSNext.ServiceApi.OData;
using HKH.Mef2.Integration;

namespace EIMSNext.ServiceApi.ODataControllers
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
	public class DepartmentController(IResolver resolver) : ODataController<Department, DepartmentViewModel,DepartmentRequest>(resolver)
	{
    }
}
