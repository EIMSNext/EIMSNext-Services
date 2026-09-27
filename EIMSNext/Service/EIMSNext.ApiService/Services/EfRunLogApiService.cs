using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Common;
using EIMSNext.Service.Contracts;
using EIMSNext.Entities;
using HKH.Mef2.Integration;
using Microsoft.EntityFrameworkCore;

namespace EIMSNext.ApiService
{
    /// <summary>
    /// 数据流运行日志聚合服务。
    /// 负责一次运行（Ef_RunLog）的列表/详情查询，详情中包含该次运行下所有节点执行记录（Ef_RunLogNode）。
    /// </summary>
    public class EfRunLogApiService : ApiServiceBase
    {
        /// <summary>
        /// 执行 EfRunLogApiService 操作。
        /// </summary>
        public EfRunLogApiService(IResolver resolver) : base(resolver)
        {
        }

        private IEfRunLogService RunLogService => Resolver.GetService<IEfRunLogService, Ef_RunLog>();
        private IEfRunLogNodeService RunLogNodeService => Resolver.GetService<IEfRunLogNodeService, Ef_RunLogNode>();

        /// <summary>
        /// 获取Runs。
        /// </summary>
        public async Task<(long total, IReadOnlyList<Ef_RunLog> items)> GetRunsAsync(
            string eventFlowId,
            long? startTime,
            long? endTime,
            bool? success,
            int skip,
            int top)
        {
            EnsureCanManageEventFlow(eventFlowId);

            var corpId = IdentityContext.CurrentCorpId;

            var query = RunLogService.All().Where(x =>
                x.CorpId == corpId &&
                x.EventFlowId == eventFlowId &&
                !x.DeleteFlag);

            if (startTime.HasValue)
            {
                query = query.Where(x => x.TriggerTime >= startTime.Value);
            }

            if (endTime.HasValue)
            {
                query = query.Where(x => x.TriggerTime <= endTime.Value);
            }

            if (success.HasValue)
            {
                query = query.Where(x => x.Success == success.Value);
            }

            var total = await query.LongCountAsync();
            var items = await query
                .OrderByDescending(x => x.TriggerTime)
                .Skip(skip)
                .Take(top)
                .ToListAsync();

            return (total, items);
        }

        /// <summary>
        /// 获取RunDetail。
        /// </summary>
        public async Task<EfRunLogDetail?> GetRunDetailAsync(string runLogId)
        {
            var run = await RunLogService.GetAsync(runLogId);
            if (run == null || run.CorpId != IdentityContext.CurrentCorpId || run.DeleteFlag)
            {
                return null;
            }

            Resolver.Resolve<TenantAccessEvaluator>().EnsureCanManageApp(run.AppId);

            var corpId = IdentityContext.CurrentCorpId;
            var nodes = await RunLogNodeService.All()
                .Where(x => x.RunLogId == runLogId && x.CorpId == corpId)
                .OrderBy(x => x.StartTime)
                .ToListAsync();

            return new EfRunLogDetail
            {
                Run = run,
                Nodes = nodes,
                ExecutedNodeIds = nodes.Select(x => x.NodeId).Distinct().ToList(),
                FailedNodeIds = nodes.Where(x => !x.Success).Select(x => x.NodeId).Distinct().ToList(),
            };
        }

        private void EnsureCanManageEventFlow(string eventFlowId)
        {
            var definition = Resolver.GetService<Wf_Definition>()
                .Query(x =>
                    x.CorpId == IdentityContext.CurrentCorpId &&
                    !x.DeleteFlag &&
                    x.Id == eventFlowId &&
                    x.FlowType == FlowType.EventFlow)
                .FirstOrDefault();

            if (definition == null)
            {
                throw new BadRequestException("智能助手不存在");
            }

            Resolver.Resolve<TenantAccessEvaluator>().EnsureCanManageApp(definition.AppId);
        }
    }

    /// <summary>
    /// EfRunLogDetail。
    /// </summary>
    public class EfRunLogDetail
    {
        /// <summary>
        /// 获取或设置Run。
        /// </summary>
        public Ef_RunLog Run { get; set; } = default!;
        /// <summary>
        /// 获取或设置Nodes。
        /// </summary>
        public IReadOnlyList<Ef_RunLogNode> Nodes { get; set; } = Array.Empty<Ef_RunLogNode>();
        /// <summary>
        /// 获取或设置ExecutedNodeIds。
        /// </summary>
        public IReadOnlyList<string> ExecutedNodeIds { get; set; } = Array.Empty<string>();
        /// <summary>
        /// 获取或设置FailedNodeIds。
        /// </summary>
        public IReadOnlyList<string> FailedNodeIds { get; set; } = Array.Empty<string>();
    }
}
