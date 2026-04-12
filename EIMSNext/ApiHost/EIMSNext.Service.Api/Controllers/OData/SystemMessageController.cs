using Asp.Versioning;

using HKH.Mef2.Integration;

using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Api.OData;
using EIMSNext.Service.Entities;

namespace EIMSNext.Service.Api.Controllers.OData
{
    [ApiVersion(1.0)]
    public class SystemMessageController(IResolver resolver) : ODataController<SystemMessageApiService, SystemMessage, SystemMessageViewModel, SystemMessageRequest>(resolver)
    {
    }
}
