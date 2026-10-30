using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using EIMSNext.Core.Abstractions;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Entities;
using EIMSNext.Flow.Core;
using EIMSNext.Flow.Core.Interfaces;
using EIMSNext.Service.Contracts;
using HKH.Mef2.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Service
{
    /// <summary>
    /// EventFlow 写数据的入口。
    /// <para>
    /// EF 写数复用 UI 提交管线（FormDataService）：插入按提交处理（验必填/生成流水号/当前节点启动流程，发
    /// Submitted），修改与删除按草稿保存处理（只发 Modified/Removed）；防循环靠链
    /// （R1/R2 + 链尾级联门禁），不需要额外的写入抑制标记。
    /// </para>
    /// </summary>
    public class EfDataProcessor : IEfDataProcessor
    {
        protected IResolver Resolver { get; private set; }
        protected IServiceContext ServiceContext { get; private set; }
        protected IRepository<Wf_Definition> WfDefinitionRepository { get; private set; }
        protected IFormDataService FormDataService { get; private set; }
        protected ILogger<EfDataProcessor> Logger { get; private set; }
        protected IRepository<EventFlowNodeExecution> NodeExecutionRepository { get; private set; }
        private static string ProcessingOwner => $"{Environment.MachineName}:{Environment.ProcessId}";

        public EfDataProcessor(IResolver resolver)
        {
            this.Resolver = resolver;
            this.ServiceContext = resolver.GetServiceContext();
            this.WfDefinitionRepository = resolver.GetRepository<Wf_Definition>();
            this.FormDataService = resolver.Resolve<IFormDataService>();
            this.Logger = resolver.GetLogger<EfDataProcessor>();
            this.NodeExecutionRepository = resolver.GetRepository<EventFlowNodeExecution>();
        }

        public bool TryRestoreNode(WorkflowInstance inst, string nodeId, out EfNodeData? nodeData)
        {
            var dataContext = (EfDataContext)inst.Data;
            var executionId = string.IsNullOrWhiteSpace(dataContext.ExecutionId) ? inst.Id : dataContext.ExecutionId;
            var completionKey = BuildNodeCompletionKey(executionId, dataContext.EventFlowId, nodeId);
            // EF Core 下事务由 Ambient TransactionScope 隐式承载（同一 DbContext 实例共享连接与事务），
            var completion = NodeExecutionRepository.Find(x => x.ExecutionKey == completionKey)
                .FirstOrDefault();
            if (completion?.Status != EventFlowNodeExecutionStatus.Completed || string.IsNullOrWhiteSpace(completion.ResultSnapshot))
            {
                nodeData = null;
                return false;
            }

            nodeData = completion.ResultSnapshot.DeserializeFromJson<EfNodeData>();
            return nodeData != null;
        }

        /// <summary>
        /// 执行 EventFlow 节点的写数据动作。
        /// </summary>
        public async Task<EfNodeData> ProcessNodeAsync(WorkflowInstance inst, EfNodeData nodeData, string actionType)
        {
            if (TransactionScope.IsInTransaction && TransactionScope.IsInTransactionFor(WfDefinitionRepository.DbContext))
            {
                return await ProcessNodeCoreAsync(inst, nodeData, actionType);
            }

            return await TransactionScope.ExecuteWithRetryAsync(
                WfDefinitionRepository.DbContext,
                () => ProcessNodeCoreAsync(inst, nodeData, actionType),
                maxRetries: 1);
        }

        private async Task<EfNodeData> ProcessNodeCoreAsync(WorkflowInstance inst, EfNodeData nodeData, string actionType)
        {
            var dataContext = (EfDataContext)inst.Data;
            InitServiceContext(dataContext);
            var executionId = string.IsNullOrWhiteSpace(dataContext.ExecutionId) ? inst.Id : dataContext.ExecutionId;
            var actions = nodeData.ActionDatas
                .Select(x => new ActionFormData { State = x.State, FormData = x.FormData, Persisted = x.Persisted })
                .ToList();
            try
            {
                var restoredActions = new List<ActionFormData>();
                var now = DateTime.UtcNow.ToTimeStampMs();
                foreach (var action in actions)
                {
                    if (action.Persisted || action.State == DataState.Unchanged)
                    {
                        restoredActions.Add(action);
                        continue;
                    }

                    var targetKey = EnsureStableTargetKey(action, executionId, dataContext.EventFlowId, nodeData.NodeId, actions.IndexOf(action));
                    var executionKey = BuildExecutionKey(executionId, dataContext.EventFlowId, nodeData.NodeId, actionType, targetKey);
                    var existing = NodeExecutionRepository.Find(x => x.ExecutionKey == executionKey).FirstOrDefault();
                    if (existing?.Status == EventFlowNodeExecutionStatus.Completed && !string.IsNullOrWhiteSpace(existing.ResultSnapshot))
                    {
                        var restored = existing.ResultSnapshot.DeserializeFromJson<EfNodeData>();
                        var restoredAction = restored?.ActionDatas.FirstOrDefault();
                        if (restoredAction != null)
                        {
                            restoredActions.Add(restoredAction);
                        }
                        continue;
                    }

                    if (existing?.Status == EventFlowNodeExecutionStatus.Processing && existing.LeaseUntil > now)
                    {
                        throw new InvalidOperationException($"EventFlow 节点正在执行中: {executionKey}");
                    }

                    var execution = existing ?? new EventFlowNodeExecution
                    {
                        Id = NodeExecutionRepository.NewId(),
                        ExecutionKey = executionKey,
                        ExecutionId = executionId,
                        WorkflowInstanceId = inst.Id,
                        RunLogId = dataContext.RunLogId,
                        CorpId = dataContext.CorpId,
                        EventFlowId = dataContext.EventFlowId,
                        NodeId = nodeData.NodeId,
                        ActionType = actionType,
                        TargetKey = targetKey,
                        Ordinal = actions.IndexOf(action),
                        FormId = nodeData.FormId,
                        SingleResult = nodeData.SingleResult,
                        Status = EventFlowNodeExecutionStatus.Processing
                        ,ProcessingOwner = ProcessingOwner
                        ,ProcessingStartedTime = now
                        ,LeaseUntil = now + 300000
                        ,AttemptCount = 1
                    };
                    if (existing == null)
                    {
                        await NodeExecutionRepository.InsertAsync(execution);
                    }
                    else
                    {
                        // PostgreSQL 的 UPDATE 不能返回整行（ExecuteUpdate 不产出 RETURNING），
                        // 因此拆成「条件 UPDATE 抢锁 → 按受影响行数判定是否抢到 → 抢到后按主键回读」。
                        // 条件 UPDATE 自带行锁，多个并发执行者只有一个能拿到 affected == 1；抢不到的一方直接抛异常。
                        var affected = await NodeExecutionRepository.UpdateManyAsync(
                            x => x.Id == execution.Id
                                && x.Status == existing.Status
                                && x.LeaseUntil <= now,
                            setters => setters
                                .SetProperty(x => x.Status, EventFlowNodeExecutionStatus.Processing)
                                .SetProperty(x => x.ProcessingOwner, ProcessingOwner)
                                .SetProperty(x => x.ProcessingStartedTime, now)
                                .SetProperty(x => x.LeaseUntil, now + 300000)
                                .SetProperty(x => x.AttemptCount, existing.AttemptCount + 1));
                        if (affected == 0)
                        {
                            throw new InvalidOperationException($"EventFlow 节点已被其他请求接管: {executionKey}");
                        }

                        execution = (await NodeExecutionRepository.GetAsync(execution.Id))!;
                    }

                    // EF 写数复用 UI 提交管线：插入按提交处理（验必填/流水号/当前节点直接起流程），
                    // 修改与删除按草稿保存处理（只发 Modified/Removed）；防循环靠链。
                    switch (action.State)
                    {
                        case DataState.Inserted:
                            ServiceContext.Action = DataAction.Submit;
                            await FormDataService.AddAsync([action.FormData]);
                            break;
                        case DataState.Modified:
                            ServiceContext.Action = DataAction.Save;
                            await FormDataService.ReplaceAsync(action.FormData);
                            break;
                        case DataState.Removed:
                            ServiceContext.Action = DataAction.Save;
                            await FormDataService.DeleteAsync([action.FormData.Id]);
                            break;
                    }

                    action.Persisted = true;
                    restoredActions.Add(action);
                    now = DateTime.UtcNow.ToTimeStampMs();
                    var snapshot = new EfNodeData
                    {
                        NodeId = nodeData.NodeId,
                        FormId = nodeData.FormId,
                        SingleResult = nodeData.SingleResult,
                        ActionDatas = [action]
                    };
                    await NodeExecutionRepository.UpdateAsync(execution.Id,
                        setters => setters
                            .SetProperty(x => x.ResultSnapshot, snapshot.SerializeToJson())
                            .SetProperty(x => x.Status, EventFlowNodeExecutionStatus.Completed)
                            .SetProperty(x => x.ProcessingOwner, string.Empty)
                            .SetProperty(x => x.LeaseUntil, 0)
                            .SetProperty(x => x.CompletedTime, now));
                }

                nodeData.ActionDatas = restoredActions;
                var completionKey = BuildNodeCompletionKey(executionId, dataContext.EventFlowId, nodeData.NodeId);
                var completion = NodeExecutionRepository.Find(x => x.ExecutionKey == completionKey).FirstOrDefault();
                var completionSnapshot = nodeData.SerializeToJson();
                if (completion == null)
                {
                    await NodeExecutionRepository.InsertAsync(new EventFlowNodeExecution
                    {
                        Id = NodeExecutionRepository.NewId(),
                        ExecutionKey = completionKey,
                        ExecutionId = executionId,
                        WorkflowInstanceId = inst.Id,
                        RunLogId = dataContext.RunLogId,
                        CorpId = dataContext.CorpId,
                        EventFlowId = dataContext.EventFlowId,
                        NodeId = nodeData.NodeId,
                        ActionType = actionType,
                        TargetKey = "__node__",
                        Ordinal = int.MaxValue,
                        FormId = nodeData.FormId,
                        SingleResult = nodeData.SingleResult,
                        Status = EventFlowNodeExecutionStatus.Completed,
                        ResultSnapshot = completionSnapshot,
                        CompletedTime = DateTime.UtcNow.ToTimeStampMs()
                    });
                }
                else
                {
                    var completedTime = DateTime.UtcNow.ToTimeStampMs();
                    await NodeExecutionRepository.UpdateAsync(completion.Id,
                        setters => setters
                            .SetProperty(x => x.Status, EventFlowNodeExecutionStatus.Completed)
                            .SetProperty(x => x.ResultSnapshot, completionSnapshot)
                            .SetProperty(x => x.CompletedTime, completedTime));
                }
                return nodeData;
            }
            catch (DbUpdateException ex) when (IsExecutionKeyDuplicate(ex))
            {
                if (TryRestoreNode(inst, nodeData.NodeId, out var restored))
                {
                    return restored!;
                }

                throw;
            }
        }

        private static string BuildExecutionPrefix(string executionId, string eventFlowId, string nodeId)
            => $"{executionId}:{eventFlowId}:{nodeId}:";

        private static string BuildExecutionKey(string executionId, string eventFlowId, string nodeId, string actionType, string targetKey)
            => $"{BuildExecutionPrefix(executionId, eventFlowId, nodeId)}{actionType}:{targetKey}";

        private static string BuildNodeCompletionKey(string executionId, string eventFlowId, string nodeId)
            => BuildExecutionKey(executionId, eventFlowId, nodeId, "complete", "__node__");

        private static string EnsureStableTargetKey(ActionFormData action, string executionId, string eventFlowId, string nodeId, int ordinal)
        {
            if (action.State != DataState.Inserted || !string.IsNullOrWhiteSpace(action.FormData.Id))
            {
                return action.FormData.Id;
            }

            // 只作 ExecutionKey 的稳定成分（重跑幂等靠它定位既有执行记账），不写回 FormData.Id：
            // 目标行主键交给仓储 EnsureId 补 TSID，与 UI 提交行的 Id 风格保持一致。
            var seed = $"{executionId}:{eventFlowId}:{nodeId}:{ordinal}";
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seed))).ToLowerInvariant();
            return hash[..24];
        }

        /// <summary>
        /// 判断异常是否为 <c>EventFlowNodeExecution.ExecutionKey</c> 唯一索引冲突。
        /// </summary>
        private static bool IsExecutionKeyDuplicate(DbUpdateException exception)
        {
            var postgres = exception.InnerException as Npgsql.PostgresException
                ?? exception.InnerException?.InnerException as Npgsql.PostgresException;
            if (postgres is null || postgres.SqlState != "23505")
            {
                return false;
            }

            return postgres.ConstraintName?.Contains("EventFlowNodeExecution", StringComparison.OrdinalIgnoreCase) == true
                || postgres.MessageText.Contains("eventflownodeexecution", StringComparison.OrdinalIgnoreCase);
        }

        private void InitServiceContext(EfDataContext dataContext)
        {
            ServiceContext.CorpId = dataContext.CorpId;
            ServiceContext.UserId = dataContext.UserId;
            ServiceContext.AccessToken = dataContext.AccessToken;
            ServiceContext.Operator = dataContext.WfStarter ?? Operator.Empty;
            ServiceContext.Action = DataAction.None;
            ServiceContext.Chain = dataContext.Chain;
        }
    }
}
