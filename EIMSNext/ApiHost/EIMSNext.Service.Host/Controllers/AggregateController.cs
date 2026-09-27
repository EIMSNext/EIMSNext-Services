using System.Threading.Tasks;
using Asp.Versioning;
using EIMSNext.ApiHost.Controllers;
using EIMSNext.ApiHost.Extensions;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.Common;
using EIMSNext.Core.Query;
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
        public async Task<IActionResult> Calucate([FromBody] DashboardAggregateRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ItemId))
                return BadRequest();

            if (IdentityContext.IdentityType == IdentityType.Public)
            {
                request.Filter.ClearValueExpressions();
            }
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
        public async Task<IActionResult> Count([FromBody] DashboardAggregateRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ItemId))
                return BadRequest();

            if (IdentityContext.IdentityType == IdentityType.Public)
            {
                request.Filter.ClearValueExpressions();
            }
            var count = await ApiService.Count(request);
            return ApiResult.Success(count).ToActionResult();
        }

        [Permission(Operation = Operation.Read)]
        [HttpPost("preview")]
        public async Task<IActionResult> Preview([FromBody] DashboardAggregatePreviewRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ItemId) || string.IsNullOrWhiteSpace(request.Details))
                return BadRequest();

            var cursor = await ApiService.Preview(request);
            if (cursor == null) return ApiResult.Fail(-1, "没有数据").ToActionResult();
            return ApiResult.Success(await cursor.ToListAsync()).ToActionResult();
        }

        [Permission(Operation = Operation.Read)]
        [HttpPost("preview/$count")]
        public async Task<IActionResult> PreviewCount([FromBody] DashboardAggregatePreviewRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ItemId) || string.IsNullOrWhiteSpace(request.Details))
                return BadRequest();
            return ApiResult.Success(await ApiService.PreviewCount(request)).ToActionResult();
        }
    }
}
