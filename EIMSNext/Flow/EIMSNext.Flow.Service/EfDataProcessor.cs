using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using EIMSNext.Core.Abstractions;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Entities;
using EIMSNext.Flow.Core;
using EIMSNext.Flow.Core.Interfaces;
using EIMSNext.Service.Contracts;
using HKH.Mef2.Integration;
using Microsoft.Extensions.Logging;
using WorkflowCore.Interface;
using WorkflowCore.Models;
using MongoDB.Driver;

namespace EIMSNext.Flow.Service
{
    /// <summary>
    /// EventFlow 写数据的入口。
    /// <para>
    /// 重要约束：eventFlow 通过本处理器写入的 <see cref="FormData"/> 使用
    /// <see cref="DataAction.EventFlow"/> 而非 <see cref="DataAction.Submit"/>，
    /// 因此 <see cref="FormDataService.BeforeAdd"/> 中 <c>ResolveSerialNumbers</c>
    /// 不会触发——这意味着 eventFlow 插入的 <c>serialno</c> 类型字段会留空。
    /// </para>
    /// <para>
    /// 这是有意为之：eventFlow 通常用于子表/批量/历史回写，让流水号在子表内自增会与
    /// 主表单号冲突。若业务需要让 eventFlow 写入的记录也带流水号，请在该节点之前的
    /// 公式/赋值中显式计算序列值，或将上游 Action 改为 <c>Submit</c>。
    /// </para>
    /// </summary>
    public class EfDataProcessor : IEfDataProcessor
    {
        protected IResolver Resolver { get; private set; }
        protected IServiceContext ServiceContext { get; private set; }
        protected IRepository<Wf_Definition> WfDefinitionRepository { get; private set; }
        protected IFormDataService FormDataService { get; private set; }
        protected IWorkflowHost WorkflowHost { get; private set; }
        protected ILogger<EfDataProcessor> Logger { get; private set; }
        protected IRepository<EventFlowNodeExecution> NodeExecutionRepository { get; private set; }
        private static string ProcessingOwner => $"{Environment.MachineName}:{Environment.ProcessId}";

        public EfDataProcessor(IResolver resolver)
        {
            this.Resolver = resolver;
            this.ServiceContext = resolver.GetServiceContext();
            this.WfDefinitionRepository = resolver.GetRepository<Wf_Definition>();
            this.FormDataService = resolver.Resolve<IFormDataService>();
            this.WorkflowHost = resolver.Resolve<IWorkflowHost>();
            this.Logger = resolver.GetLogger<EfDataProcessor>();
            this.NodeExecutionRepository = resolver.GetRepository<EventFlowNodeExecution>();
        }

        public bool TryRestoreNode(WorkflowInstance inst, string nodeId, out EfNodeData? nodeData)
        {
            var dataContext = (EfDataContext)inst.Data;
            var executionId = string.IsNullOrWhiteSpace(dataContext.ExecutionId) ? inst.Id : dataContext.ExecutionId;
            var completionKey = BuildNodeCompletionKey(executionId, dataContext.EventFlowId, nodeId);
            var completion = NodeExecutionRepository.Find(x => x.ExecutionKey == completionKey, MongoTransactionScope.Transaction)
                .FirstOrDefault();
            if (completion?.Status != EventFlowNodeExecutionStatus.Completed || string.IsNullOrWhiteSpace(completion.ResultSnapshot))
            {
                nodeData = null;
                return false;
            }

            nodeData = completion.ResultSnapshot.DeserializeFromJson<EfNodeData>();
            return nodeData != null;
        }

        public EfNodeData ProcessNode(WorkflowInstance inst, EfNodeData nodeData, string actionType)
        {
            var dataContext = (EfDataContext)inst.Data;
            InitServiceContext(dataContext);
            var executionId = string.IsNullOrWhiteSpace(dataContext.ExecutionId) ? inst.Id : dataContext.ExecutionId;
            var actions = nodeData.ActionDatas;
            try
            {
                using var scope = WfDefinitionRepository.NewTransactionScope();
                var session = scope.SessionHandle;
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
                    var existing = NodeExecutionRepository.Find(x => x.ExecutionKey == executionKey, session).FirstOrDefault();
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
                        NodeExecutionRepository.Insert(execution, session);
                    }
                    else
                    {
                        var claimFilter = Builders<EventFlowNodeExecution>.Filter.And(
                            Builders<EventFlowNodeExecution>.Filter.Eq(x => x.Id, existing.Id),
                            Builders<EventFlowNodeExecution>.Filter.Eq(x => x.Status, existing.Status),
                            Builders<EventFlowNodeExecution>.Filter.Lte(x => x.LeaseUntil, now));
                        var claimUpdate = Builders<EventFlowNodeExecution>.Update
                            .Set(x => x.Status, EventFlowNodeExecutionStatus.Processing)
                            .Set(x => x.ProcessingOwner, ProcessingOwner)
                            .Set(x => x.ProcessingStartedTime, now)
                            .Set(x => x.LeaseUntil, now + 300000)
                            .Inc(x => x.AttemptCount, 1);
                        var claimOptions = new FindOneAndUpdateOptions<EventFlowNodeExecution>
                        {
                            ReturnDocument = ReturnDocument.After
                        };
                        execution = session == null
                            ? NodeExecutionRepository.Collection.FindOneAndUpdate(claimFilter, claimUpdate, claimOptions)
                            : NodeExecutionRepository.Collection.FindOneAndUpdate(session, claimFilter, claimUpdate, claimOptions);
                        if (execution == null)
                        {
                            throw new InvalidOperationException($"EventFlow 节点已被其他请求接管: {executionKey}");
                        }
                    }

                    switch (action.State)
                    {
                        case DataState.Inserted:
                            FormDataService.Add([action.FormData], session);
                            break;
                        case DataState.Modified:
                            FormDataService.Replace(action.FormData, session);
                            break;
                        case DataState.Removed:
                            FormDataService.Delete([action.FormData.Id], session);
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
                    NodeExecutionRepository.Update(execution.Id,
                        Builders<EventFlowNodeExecution>.Update
                            .Set(x => x.ResultSnapshot, snapshot.SerializeToJson())
                            .Set(x => x.Status, EventFlowNodeExecutionStatus.Completed)
                            .Set(x => x.ProcessingOwner, string.Empty)
                            .Set(x => x.LeaseUntil, 0)
                            .Set(x => x.CompletedTime, now),
                        upsert: false,
                        session: session);
                }

                nodeData.ActionDatas = restoredActions;
                var completionKey = BuildNodeCompletionKey(executionId, dataContext.EventFlowId, nodeData.NodeId);
                var completion = NodeExecutionRepository.Find(x => x.ExecutionKey == completionKey, session).FirstOrDefault();
                var completionSnapshot = nodeData.SerializeToJson();
                if (completion == null)
                {
                    NodeExecutionRepository.Insert(new EventFlowNodeExecution
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
                    }, session);
                }
                else
                {
                    NodeExecutionRepository.Update(completion.Id,
                        Builders<EventFlowNodeExecution>.Update
                            .Set(x => x.Status, EventFlowNodeExecutionStatus.Completed)
                            .Set(x => x.ResultSnapshot, completionSnapshot)
                            .Set(x => x.CompletedTime, DateTime.UtcNow.ToTimeStampMs()),
                        upsert: false,
                        session: session);
                }
                scope.CommitTransaction();
                return nodeData;
            }
            catch (MongoWriteException ex) when (IsExecutionKeyDuplicate(ex))
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

            var seed = $"{executionId}:{eventFlowId}:{nodeId}:{ordinal}";
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seed))).ToLowerInvariant();
            action.FormData.Id = hash[..24];
            return action.FormData.Id;
        }

        private static bool IsExecutionKeyDuplicate(MongoWriteException exception)
        {
            var message = exception.WriteError?.Message ?? exception.Message;
            return exception.WriteError?.Category == ServerErrorCategory.DuplicateKey
                && message.Contains("eventflownodeexecution", StringComparison.OrdinalIgnoreCase);
        }

        public void Process(WorkflowInstance inst)
        {
            var dataContext = (EfDataContext)inst.Data;

            InitServiceContext(dataContext);

            //TODO: 流程表单，在创建后应自动提交
            foreach (var action in dataContext.NodeDatas.Values.SelectMany(x => x.ActionDatas)
                .Where(x => x.State == DataState.Inserted && !x.WorkflowStarted))
            {
                var x = action.FormData;
                try
                {
                    var formDef = dataContext.FormDefs[x.FormId];
                    if (formDef.UsingWorkflow)
                    {
                        var data = new WfDataContext(x.CorpId ?? "", dataContext.UserId, dataContext.AccessToken, x.AppId, x.FormId, x.Id, x.CreateBy, dataContext.EfCascade, dataContext.EventIds);
                        WorkflowHost.StartWorkflow(x.FormId, data.ToExpando());
                        action.WorkflowStarted = true;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "EventFlow自动提交表单失败。FormDataId={FormDataId}", x.Id);
                }
            }

            foreach (var node in dataContext.NodeDatas.Values)
            {
                node.ActionDatas = node.ActionDatas;
            }
        }

        private void InitServiceContext(EfDataContext dataContext)
        {
            ServiceContext.CorpId = dataContext.CorpId;
            ServiceContext.UserId = dataContext.UserId;
            ServiceContext.AccessToken = dataContext.AccessToken;
            ServiceContext.Operator = dataContext.WfStarter ?? Operator.Empty;
            ServiceContext.Action = DataAction.EventFlow;
        }
    }
}
