using Asp.Versioning;
using EIMSNext.ApiService;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Host.Authorization;
using EIMSNext.Service.Host.OData;
using HKH.Mef2.Integration;

namespace EIMSNext.Service.Host.Controllers.OData
{
    [ApiVersion(1.0)]
    [IdentityType(IdentityTypeDefaults.Authenticated)]
    public class ECoinPriceController(IResolver resolver)
        : ReadOnlyODataController<ECoinPriceApiService, ECoinPrice, ECoinPriceViewModel>(resolver)
    {
    }
}
