using Asp.Versioning;

using EIMSNext.ApiHost.Controllers;
using EIMSNext.ApiHost.Extensions;
using EIMSNext.ApiService;
using EIMSNext.Common;

using HKH.Mef2.Integration;

using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.Service.Host.Controllers
{
    [ApiVersion(1.0)]
    public class WorkbenchController(IResolver resolver) : MefControllerBase(resolver)
    {
        private readonly WorkbenchQueryApiService _workbenchQueryApiService = resolver.Resolve<WorkbenchQueryApiService>();

        [HttpGet("Catalog")]
        public IActionResult GetCatalog()
        {
            return ApiResult.Success(_workbenchQueryApiService.GetCatalog()).ToActionResult();
        }

        [HttpGet("ChartItem/{dashboardItemId}")]
        public IActionResult GetChartItem(string dashboardItemId)
        {
            var item = _workbenchQueryApiService.GetChartItem(dashboardItemId);
            if (item == null)
            {
                return ApiResult.Fail(404, "图表不存在或无权限").ToActionResult();
            }

            return ApiResult.Success(item).ToActionResult();
        }
    }
}
