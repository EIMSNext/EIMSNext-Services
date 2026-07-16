using Asp.Versioning;

using HKH.Mef2.Integration;

using EIMSNext.ApiService;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Host.OData;
using EIMSNext.Service.Entities;

namespace EIMSNext.Service.Host.Controllers.OData
{
    [ApiVersion(1.0)]
    public class SystemMessageController(IResolver resolver) : ReadOnlyODataController<SystemMessageApiService, SystemMessage, SystemMessageViewModel>(resolver)
    {
    }
}
