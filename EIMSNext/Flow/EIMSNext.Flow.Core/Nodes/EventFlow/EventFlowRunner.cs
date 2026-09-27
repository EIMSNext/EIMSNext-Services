
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Common.Extensions;
using EIMSNext.Entities;
using EIMSNext.Flow.Core.Interfaces;
using EIMSNext.Scripting;

using HKH.Mef2.Integration;

using Microsoft.Extensions.Logging;

using MongoDB.Driver;

using WorkflowCore.Interface;

namespace EIMSNext.Flow.Core.Nodes
{
    public class EventFlowRunner : IEventFlowRunner
    {
        private readonly IResolver _resolver;

        public EventFlowRunner(IResolver resolver)
        {
            _resolver = resolver;
            ScriptEngine = resolver.Resolve<IScriptEngine>();
            Logger = resolver.GetLogger<EventFlowRunner>();
        }

        protected ISyncWorkflowRunner SyncWfRunner => _resolver.Resolve<ISyncWorkflowRunner>();
        protected IScriptEngine ScriptEngine { get; private set; }
        protected ILogger<EventFlowRunner> Logger { get; private set; }
        private IRepository<WorkflowTransitionExecution> TransitionRepository => _resolver.Resolve<IRepository<WorkflowTransitionExecution>>();


        public bool IsMeet(Wf_Definition eventFlow, FormData data)
        {
            if (eventFlow.EventSource == EventSourceType.Form)
            {
                var triggerSetting = eventFlow.Metadata.Steps.First().EfNodeSetting?.TriggerSetting;

                if (!string.IsNullOrEmpty(triggerSetting?.Condition))
                {
                    return ScriptEngine.Evaluate<bool>(triggerSetting.Condition, data.ToScriptData()).Value;
                }
            }

            return true;
        }

        public async Task<EfExecResult> RunAsync(EfRunParameter paramter)
        {
            var execResult = new EfExecResult();
            if (string.IsNullOrWhiteSpace(paramter.ExecutionId))
            {
                paramter.WithExecutionId(Guid.NewGuid().ToString("N"));
            }
            if (paramter.Cascade == CascadeMode.Never || (paramter.Cascade == CascadeMode.Specified && string.IsNullOrEmpty(paramter.EventIds)))
            {
                return execResult;
            }

            var transitionMode = paramter.WorkflowTransition
                && !string.IsNullOrWhiteSpace(paramter.WfNodeId)
                && !string.IsNullOrWhiteSpace(paramter.NodeAction);
            var session = MongoTransactionScope.Transaction;
            if (transitionMode)
            {
                var transition = TransitionRepository.Find(x => x.ExecutionId == paramter.ExecutionId, session).FirstOrDefault();
                if (transition?.Status == WorkflowTransitionStatus.Completed)
                {
                    return execResult;
                }

                var now = DateTime.UtcNow.ToTimeStampMs();
                TransitionRepository.UpdateMany(
                    TransitionRepository.FilterBuilder.Eq(x => x.ExecutionId, paramter.ExecutionId),
                    Builders<WorkflowTransitionExecution>.Update
                        .SetOnInsert(x => x.Id, TransitionRepository.NewId())
                        .SetOnInsert(x => x.ExecutionId, paramter.ExecutionId)
                        .SetOnInsert(x => x.WorkflowInstanceId, paramter.WorkflowInstanceId)
                        .SetOnInsert(x => x.CorpId, paramter.Data.CorpId ?? string.Empty)
                        .SetOnInsert(x => x.WfNodeId, paramter.WfNodeId)
                        .SetOnInsert(x => x.NodeAction, paramter.NodeAction ?? string.Empty)
                        .SetOnInsert(x => x.CreateTime, now)
                        .Set(x => x.Status, WorkflowTransitionStatus.Running)
                        .Set(x => x.UpdateTime, DateTime.UtcNow.ToTimeStampMs()),
                    upsert: true,
                    session: session);
            }

            var repository = _resolver.Resolve<IRepository<Wf_Definition>>();
            var candidates = repository.Find(x => x.CorpId == paramter.Data.CorpId
                && x.FlowType == FlowType.EventFlow
                && !x.DeleteFlag
                && !x.Disabled
                && x.EventSetting != null
                && x.EventSource == paramter.EventSource, session).ToList();

            if (!string.IsNullOrEmpty(paramter.EventFlowId))
            {
                candidates = candidates.Where(x => x.Id == paramter.EventFlowId).ToList();
            }

            foreach (var eventFlow in candidates.Where(x => IsRunnableEventFlow(x, paramter)))
            {
                var result = await RunSingleAsync(eventFlow, paramter);
                execResult.EfInstance = result.EfInstance ?? execResult.EfInstance;

                if (!result.Success)
                {
                    execResult.Error = string.IsNullOrEmpty(execResult.Error)
                        ? result.Error
                        : $"{execResult.Error}; {result.Error}";
                }
            }

            if (transitionMode)
            {
                if (string.IsNullOrEmpty(execResult.Error))
                {
                    TransitionRepository.UpdateMany(
                        TransitionRepository.FilterBuilder.Eq(x => x.ExecutionId, paramter.ExecutionId),
                        Builders<WorkflowTransitionExecution>.Update
                            .Set(x => x.Status, WorkflowTransitionStatus.EventFlowsCompleted)
                            .Set(x => x.Error, string.Empty)
                            .Set(x => x.UpdateTime, DateTime.UtcNow.ToTimeStampMs()),
                        upsert: false,
                        session: session);

                    await MongoTransactionScope.RegisterAfterCommitAsync(() =>
                    {
                        TransitionRepository.UpdateMany(
                            TransitionRepository.FilterBuilder.Eq(x => x.ExecutionId, paramter.ExecutionId),
                            Builders<WorkflowTransitionExecution>.Update
                                .Set(x => x.Status, WorkflowTransitionStatus.Completed)
                                .Set(x => x.UpdateTime, DateTime.UtcNow.ToTimeStampMs()),
                            upsert: false);
                        return Task.CompletedTask;
                    });
                }
                else
                {
                    TransitionRepository.UpdateMany(
                        TransitionRepository.FilterBuilder.Eq(x => x.ExecutionId, paramter.ExecutionId),
                        Builders<WorkflowTransitionExecution>.Update
                            .Set(x => x.Status, WorkflowTransitionStatus.Failed)
                            .Set(x => x.Error, execResult.Error ?? string.Empty)
                            .Set(x => x.UpdateTime, DateTime.UtcNow.ToTimeStampMs()),
                        upsert: false,
                        session: session);
                }
            }

            return execResult;
        }

        private bool IsRunnableEventFlow(Wf_Definition eventFlow, EfRunParameter paramter)
        {
            if (!IsCascadeAllowed(eventFlow, paramter))
            {
                return false;
            }

            if (!IsSourceMatched(eventFlow, paramter))
            {
                return false;
            }

            if (!IsEventMatched(eventFlow, paramter))
            {
                return false;
            }

            var setting = eventFlow.EventSetting!;
            if (!string.IsNullOrEmpty(setting.WfNodeId)
                && !string.Equals(setting.WfNodeId, paramter.WfNodeId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(setting.NodeAction)
                && !string.Equals(setting.NodeAction, paramter.NodeAction, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!IsChangeFieldsMatched(eventFlow, paramter))
            {
                return false;
            }

            return IsMeet(eventFlow, paramter.Data);
        }

        private static bool IsCascadeAllowed(Wf_Definition eventFlow, EfRunParameter paramter)
        {
            return paramter.Cascade == CascadeMode.NotSet
                || paramter.Cascade == CascadeMode.All
                || IsSpecified(paramter.EventIds, eventFlow.Id);
        }

        private static bool IsSourceMatched(Wf_Definition eventFlow, EfRunParameter paramter)
        {
            return string.IsNullOrEmpty(eventFlow.SourceId)
                || string.Equals(paramter.Data.FormId, eventFlow.SourceId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsEventMatched(Wf_Definition eventFlow, EfRunParameter paramter)
        {
            var configured = eventFlow.EventSetting!.EventType;
            return paramter.EventType == EventType.None
                ? configured == EventType.None
                : configured.HasFlag(paramter.EventType);
        }

        private static bool IsChangeFieldsMatched(Wf_Definition eventFlow, EfRunParameter paramter)
        {
            if (paramter.EventType != EventType.Modified)
            {
                return true;
            }

            var configured = eventFlow.Metadata.Steps
                .FirstOrDefault()?
                .EfNodeSetting?
                .TriggerSetting?
                .ChangeFields;

            if (configured == null || configured.Count == 0)
            {
                return true;
            }

            if (paramter.ChangeFields == null || paramter.ChangeFields.Count == 0)
            {
                return false;
            }

            return configured
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Intersect(paramter.ChangeFields, StringComparer.OrdinalIgnoreCase)
                .Any();
        }

        private static bool IsSpecified(string? eventIds, string eventFlowId)
        {
            if (string.IsNullOrWhiteSpace(eventIds))
            {
                return false;
            }

            return eventIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(eventFlowId, StringComparer.OrdinalIgnoreCase);
        }

        private async Task<EfExecResult> RunSingleAsync(Wf_Definition eventFlow, EfRunParameter paramter)
        {
            var execResult = new EfExecResult();
            var runLogRepository = _resolver.Resolve<IRepository<Ef_RunLog>>();
            var startTime = DateTime.UtcNow.ToTimeStampMs();
            var runLog = CreateRunLog(eventFlow, paramter, startTime);
            try
            {
                runLogRepository.Insert(runLog);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "写入数据流运行日志失败。EventFlowId={EventFlowId}", eventFlow.Id);
                runLog = null;
            }

            var ctx = new EfDataContext()
            {
                CorpId = paramter.Data.CorpId ?? "",
                UserId = paramter.UserId,
                AccessToken = paramter.AccessToken,
                AppId = paramter.Data.AppId,
                EventFlowId = eventFlow.Id,
                RunLogId = runLog?.Id ?? string.Empty,
                ExecutionId = string.IsNullOrWhiteSpace(paramter.ExecutionId)
                    ? runLog?.Id ?? Guid.NewGuid().ToString("N")
                    : paramter.ExecutionId,
                WorkflowInstanceId = paramter.WorkflowInstanceId,
                WorkflowTransition = paramter.WorkflowTransition
                    && !string.IsNullOrWhiteSpace(paramter.WfNodeId)
                    && !string.IsNullOrWhiteSpace(paramter.NodeAction),
                WfNodeId = paramter.WfNodeId,
                NodeAction = paramter.NodeAction ?? string.Empty,
                FormId = paramter.Data.FormId,
                DataId = paramter.Data.Id,
                TriggerData = paramter.Data,
                WfStarter = paramter.Starter,
                EfCascade = eventFlow.EventSetting!.CascadeMode,
                EventIds = eventFlow.EventSetting.SpecifiedEvents
            };

            try
            {
                var efInst = await SyncWfRunner.RunWorkflowSync(eventFlow.ExternalId, 1, ctx, "", CancellationToken.None, false);
                var efDataContext = efInst.Data as EfDataContext;
                execResult.EfInstance = efInst;
                execResult.Error = efDataContext?.ErrMsg;
                UpdateRunLog(runLogRepository, runLog, efInst.Id, string.IsNullOrEmpty(execResult.Error), execResult.Error);
            }
            catch (Exception ex)
            {
                execResult.Error = ex.Message;
                UpdateRunLog(runLogRepository, runLog, string.Empty, false, ex.Message);
            }

            return execResult;
        }

        private static Ef_RunLog CreateRunLog(Wf_Definition eventFlow, EfRunParameter paramter, long startTime)
        {
            var triggerSetting = eventFlow.Metadata.Steps.FirstOrDefault()?.EfNodeSetting?.TriggerSetting;
            return new Ef_RunLog
            {
                Id = string.Empty,
                CorpId = paramter.Data.CorpId,
                AppId = eventFlow.AppId,
                EventFlowId = eventFlow.Id,
                EventFlowName = eventFlow.Name,
                EventFlowVersion = eventFlow.Version,
                TriggerKind = triggerSetting?.TriggerKind ?? GuessTriggerKind(paramter.EventSource),
                EventSource = paramter.EventSource,
                EventType = paramter.EventType,
                TriggerBy = paramter.Starter ?? Operator.Empty,
                TriggerTime = startTime,
                StartTime = startTime,
                Success = false,
                CreateBy = paramter.Starter ?? Operator.Empty,
                CreateTime = startTime,
            };
        }

        private void UpdateRunLog(IRepository<Ef_RunLog> repository, Ef_RunLog? runLog, string wfInstanceId, bool success, string? errMsg)
        {
            if (runLog == null)
            {
                return;
            }

            try
            {
                var endTime = DateTime.UtcNow.ToTimeStampMs();
                if (!string.IsNullOrEmpty(wfInstanceId))
                {
                    runLog.WfInstanceId = wfInstanceId;
                }

                runLog.EndTime = endTime;
                runLog.Success = success;
                runLog.ErrMsg = errMsg ?? string.Empty;
                runLog.UpdateTime = endTime;
                repository.Replace(runLog);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "更新数据流运行日志失败。RunLogId={RunLogId}", runLog.Id);
            }
        }

        private static EventFlowTriggerKind GuessTriggerKind(EventSourceType eventSource)
        {
            return eventSource switch
            {
                EventSourceType.Schedule => EventFlowTriggerKind.Schedule,
                EventSourceType.Http => EventFlowTriggerKind.Http,
                _ => EventFlowTriggerKind.Form,
            };
        }
    }
}
