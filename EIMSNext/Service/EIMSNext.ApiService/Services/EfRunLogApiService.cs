using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Common;
using EIMSNext.Service.Contracts;
using EIMSNext.Entities;
using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Common.Extensions;
using EIMSNext.Json;
using HKH.Mef2.Integration;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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

            if (run.RetryInProgress && (run.RetryLeaseUntil == null || run.RetryLeaseUntil <= DateTime.UtcNow.ToTimeStampMs()))
            {
                run.RetryInProgress = false;
            }

            return new EfRunLogDetail
            {
                Run = run,
                Nodes = nodes,
                ExecutedNodeIds = nodes.Select(x => x.NodeId).Distinct().ToList(),
                FailedNodeIds = nodes.Where(x => !x.Success).Select(x => x.NodeId).Distinct().ToList(),
            };
        }

        /// <summary>
        /// 领取重试租约（CAS 乐观锁）并同事务投递重试消息；租约冲突抛 <see cref="ConflictException"/>。
        /// </summary>
        public async Task<EventFlowRunTaskArgs> QueueRetryAsync(string runLogId, string retryNodeId)
        {
            var runLogRepository = Resolver.GetRepository<Ef_RunLog>();
            await using var scope = new TransactionScope(runLogRepository.DbContext);
            var run = runLogRepository.Get(runLogId);
            if (run == null || run.CorpId != IdentityContext.CurrentCorpId || run.DeleteFlag)
                throw new NotFoundException("运行日志不存在");

            Resolver.Resolve<TenantAccessEvaluator>().EnsureCanManageApp(run.AppId);
            if (run.Success)
                throw new BadRequestException("运行成功的 EF 不允许重试");

            var node = await RunLogNodeService.All()
                .Where(x => x.RunLogId == runLogId
                    && x.CorpId == IdentityContext.CurrentCorpId
                    && x.NodeId == retryNodeId
                    && !x.Success)
                .FirstOrDefaultAsync();
            if (node == null)
                throw new BadRequestException("只能重试当前运行中的失败节点");

            var context = string.IsNullOrWhiteSpace(run.RetryContextJson)
                ? null
                : run.RetryContextJson.DeserializeFromJson<EfRunRetryContext>();
            if (context == null || string.IsNullOrWhiteSpace(context.ExecutionId))
                throw new BadRequestException("该运行缺少可重试上下文");

            var retryAttemptId = TsidIdGenerator.NewId();
            var now = DateTime.UtcNow.ToTimeStampMs();
            // Flow.Host 内部重试是同步执行，租约要覆盖常规长节点执行时间；
            // 超时后仍允许人工重新领取，避免基础设施永久卡住。
            var leaseUntil = now + (long)TimeSpan.FromMinutes(30).TotalMilliseconds;
            var claimed = await runLogRepository.UpdateManyAsync(
                x => x.Id == runLogId
                    && (!x.RetryInProgress || x.RetryLeaseUntil == null || x.RetryLeaseUntil <= now),
                setters => setters
                    .SetProperty(x => x.RetryInProgress, true)
                    .SetProperty(x => x.RetryAttemptId, retryAttemptId)
                    .SetProperty(x => x.RetryLeaseUntil, leaseUntil));
            if (claimed != 1)
                throw new ConflictException("该运行正在重试中");

            var message = new EventFlowRunTaskArgs
            {
                CorpId = run.CorpId ?? string.Empty,
                EventFlowId = run.EventFlowId,
                ExecutionId = context.ExecutionId,
                RunLogId = runLogId,
                RetryAttemptId = retryAttemptId,
                RetryNodeId = retryNodeId,
                AppId = run.AppId,
                DataId = context.DataId,
                EventSource = run.EventSource,
                EventType = run.EventType,
                WfNodeId = context.WfNodeId,
                NodeAction = context.NodeAction,
                ChangeFields = context.ChangeFields,
                ChainJson = context.ChainJson,
                MatchAllEventFlows = false,
                StarterId = run.TriggerBy?.Id,
                StarterCode = run.TriggerBy?.Value,
                StarterName = run.TriggerBy?.Label,
            };

            await Resolver.Resolve<ITransactionalOutboxPublisher>().EnqueueRequiredAsync(message);
            await scope.CommitTransactionAsync();
            return message;
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
