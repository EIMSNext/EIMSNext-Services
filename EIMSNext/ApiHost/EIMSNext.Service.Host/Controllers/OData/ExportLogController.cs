using Asp.Versioning;
using EIMSNext.ApiService;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Host.OData;
using HKH.Mef2.Integration;

namespace EIMSNext.Service.Host.Controllers.OData
{
    [ApiVersion(1.0)]
    public class ExportLogController(IResolver resolver)
        : ReadOnlyODataController<ExportLogApiService, ExportLog, ExportLogViewModel>(resolver)
    {
    }
}
