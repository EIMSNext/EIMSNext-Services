using System.Threading.Tasks;
using Asp.Versioning;
using EIMSNext.ApiHost.Controllers;
using EIMSNext.ApiHost.Extension;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModel;
using EIMSNext.Common;
using HKH.Mef2.Integration;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

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
        public async Task<IActionResult> Calucate([FromBody] AggCalcRequest request)
        {
            if (request == null || request.DataSource == null || request.Dimensions?.Count == 0 || request.Metrics?.Count == 0)
                return BadRequest();
            var cursor = await ApiService.Calucate(request);
            if ((cursor == null))
            {
                return ApiResult.Fail(-1, "没有数据").ToActionResult();
            }

            var data = await cursor.ToListAsync();
            return ApiResult.Success(data).ToActionResult();
        }
    }
}
