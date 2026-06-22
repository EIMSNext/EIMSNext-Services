using Asp.Versioning;
using EIMSNext.ApiHost.Controllers;
using EIMSNext.ApiService;
using EIMSNext.Service.Host.Authorization;
using HKH.Mef2.Integration;
using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.Service.Host.Controllers
{
    /// <summary>
    /// 数据流运行日志聚合接口。
    /// </summary>
    [ApiVersion(1.0)]
    [IdentityType(IdentityTypeDefaults.BusinessUser)]
    public class DfRunLogController(IResolver resolver) : MefControllerBase(resolver)
    {
        private DfRunLogApiService RunLogApiService => Resolver.Resolve<DfRunLogApiService>();

        [HttpGet("Runs")]
        public async Task<IActionResult> GetRuns(
            [FromQuery] string dataflowId,
            [FromQuery] long? startTime,
            [FromQuery] long? endTime,
            [FromQuery] bool? success,
            [FromQuery] int skip = 0,
            [FromQuery] int top = 20)
        {
            if (string.IsNullOrWhiteSpace(dataflowId))
            {
                return BadRequest("dataflowId不能为空");
            }

            skip = Math.Max(0, skip);
            top = Math.Clamp(top, 1, 100);

            var (total, items) = await RunLogApiService.GetRunsAsync(dataflowId, startTime, endTime, success, skip, top);
            return Ok(new { total, items });
        }

        [HttpGet("Runs/{runLogId}")]
        public async Task<IActionResult> GetRunDetail([FromRoute] string runLogId)
        {
            if (string.IsNullOrWhiteSpace(runLogId))
            {
                return BadRequest("runLogId不能为空");
            }

            var detail = await RunLogApiService.GetRunDetailAsync(runLogId);
            if (detail == null)
            {
                return NotFound("运行日志不存在");
            }

            return Ok(detail);
        }
    }
}
