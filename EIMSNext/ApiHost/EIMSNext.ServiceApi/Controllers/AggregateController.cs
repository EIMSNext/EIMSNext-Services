using Asp.Versioning;
using EIMSNext.ApiHost.Controllers;
using EIMSNext.ApiService.Interface;
using EIMSNext.ApiService.RequestModel;
using HKH.Mef2.Integration;
using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.ServiceApi.Controllers
{
    [ApiVersion(1.0)]
    public class AggregateController : MefControllerBase
    {
        public AggregateController(IResolver resolver) : base(resolver)
        {
            ApiService = resolver.Resolve<IAggregateApiService>();
        }

        private IAggregateApiService ApiService { get; set; }

        [HttpPost("Calucate")]
        public IActionResult Calucate(AggCalcRequest request)
        {
            if (request == null || request.DataSource == null || request.Dimensions?.Count == 0 || request.Metrics?.Count == 0)
                return BadRequest();

            return Ok(ApiService.Calucate(request));
        }
    }
}
