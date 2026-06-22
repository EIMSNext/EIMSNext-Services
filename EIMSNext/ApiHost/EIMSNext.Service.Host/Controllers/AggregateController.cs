using System.Threading.Tasks;
using Asp.Versioning;
using EIMSNext.ApiHost.Controllers;
using EIMSNext.ApiHost.Extensions;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.Common;
using EIMSNext.Service.Host.Authorization;
using HKH.Mef2.Integration;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace EIMSNext.Service.Host.Controllers
{
    [ApiVersion(1.0)]
    [IdentityType(IdentityTypeDefaults.BusinessUser)]
    public class AggregateController : MefControllerBase
    {
        public AggregateController(IResolver resolver) : base(resolver)
        {
            ApiService = resolver.Resolve<IAggregateApiService>();
        }

        private IAggregateApiService ApiService { get; set; }

        [Permission(Operation = Operation.Read)]
        [IdentityType(IdentityTypeDefaults.PublicBusinessUser)]
        [PublicScope(PublicScope.DashLink)]
        [HttpPost("Calucate")]
        public async Task<IActionResult> Calucate([FromBody] AggCalcRequest request)
        {
            if (request == null || request.DataSource == null)
                return BadRequest();
            if (IdentityContext.IdentityType == IdentityType.Public && string.IsNullOrWhiteSpace(request.ItemId))
                return Forbid();

            bool isAggregate = (request.Dimensions?.Count > 0 || request.Metrics?.Count > 0);
            if (isAggregate && (request.Dimensions == null || request.Dimensions.Count == 0))
                return BadRequest("聚合请求缺少维度");

            var cursor = await ApiService.Calucate(request);
            if ((cursor == null))
            {
                return ApiResult.Fail(-1, "没有数据").ToActionResult();
            }

            var data = await cursor.ToListAsync();
            return ApiResult.Success(data).ToActionResult();
        }

        [Permission(Operation = Operation.Read)]
        [IdentityType(IdentityTypeDefaults.PublicBusinessUser)]
        [PublicScope(PublicScope.DashLink)]
        [HttpPost("$count")]
        public async Task<IActionResult> Count([FromBody] AggCalcRequest request)
        {
            if (request == null || request.DataSource == null)
                return BadRequest();
            if (IdentityContext.IdentityType == IdentityType.Public && string.IsNullOrWhiteSpace(request.ItemId))
                return Forbid();

            var count = await ApiService.Count(request);
            return ApiResult.Success(count).ToActionResult();
        }
    }
}
