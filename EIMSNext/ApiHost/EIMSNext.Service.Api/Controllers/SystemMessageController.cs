using Asp.Versioning;

using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Common;
using EIMSNext.Service.Api.Authorization;
using EIMSNext.Service.Api.Requests;
using EIMSNext.Service.Entities;

using HKH.Mef2.Integration;

using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.Service.Api.Controllers
{
    [ApiVersion(1.0)]
    public class SystemMessageController(IResolver resolver) : ApiControllerBase<SystemMessageApiService, SystemMessage, SystemMessageViewModel>(resolver)
    {
        [HttpGet("UnreadCount")]
        [Permission(Operation = Operation.Read)]
        public async Task<ActionResult> UnreadCount()
        {
            return Ok(ApiResult.Success(await ApiService.GetUnreadCountAsync()));
        }

        [HttpPost("Read")]
        [Permission(Operation = Operation.Write)]
        public async Task<ActionResult> Read([FromBody] SystemMessageReadRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Id))
            {
                return BadRequest();
            }

            await ApiService.MarkReadAsync(request.Id);
            return Ok(ApiResult.Success());
        }

        [HttpPost("ReadBatch")]
        [Permission(Operation = Operation.Write)]
        public async Task<ActionResult> ReadBatch([FromBody] DeleteBatch request)
        {
            if (request.Keys?.Count <= 0)
            {
                return BadRequest();
            }

            await ApiService.MarkReadBatchAsync(request.Keys!);
            return Ok(ApiResult.Success());
        }
    }
}
