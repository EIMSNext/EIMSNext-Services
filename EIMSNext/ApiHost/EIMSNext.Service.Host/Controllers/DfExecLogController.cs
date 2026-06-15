using Asp.Versioning;
using EIMSNext.ApiHost.Controllers;
using EIMSNext.Core.Repositories;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace EIMSNext.Service.Host.Controllers
{
    /// <summary>
    /// 数据流执行日志聚合接口。
    /// </summary>
    [ApiVersion(1.0)]
    public class DfExecLogController(IResolver resolver) : MefControllerBase(resolver)
    {
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

            var repository = Resolver.Resolve<IRepository<Df_RunLog>>();
            var fb = repository.FilterBuilder;
            var filter = fb.Eq(x => x.CorpId, IdentityContext.CurrentCorpId)
                & fb.Eq(x => x.DataflowId, dataflowId)
                & fb.Eq(x => x.DeleteFlag, false);

            if (startTime.HasValue)
            {
                filter &= fb.Gte(x => x.TriggerTime, startTime.Value);
            }

            if (endTime.HasValue)
            {
                filter &= fb.Lte(x => x.TriggerTime, endTime.Value);
            }

            if (success.HasValue)
            {
                filter &= fb.Eq(x => x.Success, success.Value);
            }

            var total = await repository.CountAsync(filter);
            var items = await repository.Collection
                .Find(filter)
                .SortByDescending(x => x.TriggerTime)
                .Skip(skip)
                .Limit(top)
                .ToListAsync();

            return Ok(new { total, items });
        }

        [HttpGet("Runs/{runLogId}")]
        public async Task<IActionResult> GetRunDetail([FromRoute] string runLogId)
        {
            if (string.IsNullOrWhiteSpace(runLogId))
            {
                return BadRequest("runLogId不能为空");
            }

            var runRepository = Resolver.Resolve<IRepository<Df_RunLog>>();
            var run = await runRepository.Collection
                .Find(x => x.Id == runLogId && x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag)
                .FirstOrDefaultAsync();
            if (run == null)
            {
                return NotFound("运行日志不存在");
            }

            var nodeRepository = Resolver.Resolve<IRepository<Df_ExecLog>>();
            var nodes = await nodeRepository.Collection
                .Find(x => x.RunLogId == runLogId && x.CorpId == IdentityContext.CurrentCorpId)
                .SortBy(x => x.StartTime)
                .ToListAsync();

            return Ok(new
            {
                run,
                nodes,
                executedNodeIds = nodes.Select(x => x.NodeId).Distinct().ToList(),
                failedNodeIds = nodes.Where(x => !x.Success).Select(x => x.NodeId).Distinct().ToList(),
            });
        }
    }
}
