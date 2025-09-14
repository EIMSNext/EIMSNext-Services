using Asp.Versioning;

using HKH.Mef2.Integration;

using EIMSNext.ServiceApi.OData;
using EIMSNext.ApiService.RequestModel;
using EIMSNext.ApiService.ViewModel;
using EIMSNext.Entity;

using Microsoft.AspNetCore.Mvc;

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
