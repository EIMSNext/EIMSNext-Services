using Asp.Versioning;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Entities;
using EIMSNext.Service.Host.OData;
using HKH.Mef2.Integration;

namespace EIMSNext.Service.Host.Controllers.OData
{
    [ApiVersion(1.0)]
    public class CrossBindingController(IResolver resolver)
        : ODataController<CrossBindingApiService, CrossBinding, CrossBindingViewModel, CrossBindingRequest>(resolver)
    {
    }
}
