using Asp.Versioning;

using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Host.OData;

using HKH.Mef2.Integration;

namespace EIMSNext.Service.Host.Controllers.OData
{
    [ApiVersion(1.0)]
    public class WorkbenchConfigController(IResolver resolver) : ODataController<WorkbenchConfigApiService, WorkbenchConfig, WorkbenchConfigViewModel, WorkbenchConfigRequest>(resolver)
    {
    }
}
