
using EIMSNext.Core;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Common.Extensions;
using EIMSNext.Service.Entities;
using EIMSNext.Flow.Core.Interfaces;
using EIMSNext.Scripting;

using HKH.Mef2.Integration;

using Microsoft.Extensions.Logging;

using MongoDB.Driver;

using WorkflowCore.Interface;

namespace EIMSNext.Flow.Core.Nodes
{
    public class DataflowRunner : IDataflowRunner
    {
        private readonly IResolver _resolver;

        public DataflowRunner(IResolver resolver)
        {
            _resolver = resolver;
            ScriptEngine = resolver.Resolve<IScriptEngine>();
            Logger = resolver.GetLogger<DataflowRunner>();
        }

        protected ISyncWorkflowRunner SyncWfRunner => _resolver.Resolve<ISyncWorkflowRunner>();
        protected IScriptEngine ScriptEngine { get; private set; }
        protected ILogger<DataflowRunner> Logger { get; private set; }


        public bool IsMeet(Wf_Definition dataflow, FormData data)
        {
            if (dataflow.EventSource == EventSourceType.Form)
            {
                var triggerSetting = dataflow.Metadata.Steps.First().DfNodeSetting?.TriggerSetting;

                if (!string.IsNullOrEmpty(triggerSetting?.Condition))
                {
                    return ScriptEngine.Evaluate<bool>(triggerSetting.Condition, data.ToScriptData()).Value;
                }
            }

            return true;
        }

        public async Task<DfExecResult> RunAsync(DfRunParamter paramter)
        {
            var execResult = new DfExecResult();
            if (paramter.Cascade == CascadeMode.Never || (paramter.Cascade == CascadeMode.Specified && string.IsNullOrEmpty(paramter.EventIds)))
            {
                return execResult;
            }

            var repository = _resolver.Resolve<IRepository<Wf_Definition>>();
            var candidates = repository.Find(x => x.CorpId == paramter.Data.CorpId
                && x.FlowType == FlowType.Dataflow
                && !x.DeleteFlag
                && !x.Disabled
                && x.EventSetting != null
                && x.EventSource == paramter.EventSource).ToList();

            if (!string.IsNullOrEmpty(paramter.DataflowId))
            {
                candidates = candidates.Where(x => x.Id == paramter.DataflowId).ToList();
            }

            foreach (var dataflow in candidates.Where(x => IsRunnableDataflow(x, paramter)))
            {
                var result = await RunSingleAsync(dataflow, paramter);
                execResult.DfInstance = result.DfInstance ?? execResult.DfInstance;

                if (!result.Success)
                {
                    execResult.Error = string.IsNullOrEmpty(execResult.Error)
                        ? result.Error
                        : $"{execResult.Error}; {result.Error}";
                }
            }

            return execResult;
        }

        private bool IsRunnableDataflow(Wf_Definition dataflow, DfRunParamter paramter)
        {
            if (!IsCascadeAllowed(dataflow, paramter))
            {
                return false;
            }

            if (!IsSourceMatched(dataflow, paramter))
            {
                return false;
            }

            if (!IsEventMatched(dataflow, paramter))
            {
                return false;
            }

            var setting = dataflow.EventSetting!;
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

            if (!IsChangeFieldsMatched(dataflow, paramter))
            {
                return false;
            }

            return IsMeet(dataflow, paramter.Data);
        }

        private static bool IsCascadeAllowed(Wf_Definition dataflow, DfRunParamter paramter)
        {
            return paramter.Cascade == CascadeMode.NotSet
                || paramter.Cascade == CascadeMode.All
                || IsSpecified(paramter.EventIds, dataflow.Id);
        }

        private static bool IsSourceMatched(Wf_Definition dataflow, DfRunParamter paramter)
        {
            return string.IsNullOrEmpty(dataflow.SourceId)
                || string.Equals(paramter.Data.FormId, dataflow.SourceId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsEventMatched(Wf_Definition dataflow, DfRunParamter paramter)
        {
            var configured = dataflow.EventSetting!.EventType;
            return paramter.EventType == EventType.None
                ? configured == EventType.None
                : configured.HasFlag(paramter.EventType);
        }

        private static bool IsChangeFieldsMatched(Wf_Definition dataflow, DfRunParamter paramter)
        {
            if (paramter.EventType != EventType.Modified)
            {
                return true;
            }

            var configured = dataflow.Metadata.Steps
                .FirstOrDefault()?
                .DfNodeSetting?
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

        private static bool IsSpecified(string? eventIds, string dataflowId)
        {
            if (string.IsNullOrWhiteSpace(eventIds))
            {
                return false;
            }

            return eventIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(dataflowId, StringComparer.OrdinalIgnoreCase);
        }

        private async Task<DfExecResult> RunSingleAsync(Wf_Definition dataflow, DfRunParamter paramter)
        {
            var execResult = new DfExecResult();
            var runLogRepository = _resolver.Resolve<IRepository<Df_RunLog>>();
            var startTime = DateTime.UtcNow.ToTimeStampMs();
            var runLog = CreateRunLog(dataflow, paramter, startTime);
            try
            {
                runLogRepository.Insert(runLog);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "写入数据流运行日志失败。DataflowId={DataflowId}", dataflow.Id);
                runLog = null;
            }

            var ctx = new DfDataContext()
            {
                CorpId = paramter.Data.CorpId ?? "",
                UserId = paramter.UserId,
                AccessToken = paramter.AccessToken,
                AppId = paramter.Data.AppId,
                DataflowId = dataflow.Id,
                RunLogId = runLog?.Id ?? string.Empty,
                FormId = paramter.Data.FormId,
                DataId = paramter.Data.Id,
                TriggerData = paramter.Data,
                WfStarter = paramter.Starter,
                DfCascade = dataflow.EventSetting!.CascadeMode,
                EventIds = dataflow.EventSetting.SpecifiedEvents
            };

            try
            {
                var dfInst = await SyncWfRunner.RunWorkflowSync(dataflow.ExternalId, 1, ctx, "", CancellationToken.None, false);
                var dfDataContext = dfInst.Data as DfDataContext;
                execResult.DfInstance = dfInst;
                execResult.Error = dfDataContext?.ErrMsg;
                UpdateRunLog(runLogRepository, runLog, dfInst.Id, string.IsNullOrEmpty(execResult.Error), execResult.Error);
            }
            catch (Exception ex)
            {
                execResult.Error = ex.Message;
                UpdateRunLog(runLogRepository, runLog, string.Empty, false, ex.Message);
            }

            return execResult;
        }

        private static Df_RunLog CreateRunLog(Wf_Definition dataflow, DfRunParamter paramter, long startTime)
        {
            var triggerSetting = dataflow.Metadata.Steps.FirstOrDefault()?.DfNodeSetting?.TriggerSetting;
            return new Df_RunLog
            {
                Id = string.Empty,
                CorpId = paramter.Data.CorpId,
                AppId = dataflow.AppId,
                DataflowId = dataflow.Id,
                DataflowName = dataflow.Name,
                DataflowVersion = dataflow.Version,
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

        private void UpdateRunLog(IRepository<Df_RunLog> repository, Df_RunLog? runLog, string wfInstanceId, bool success, string? errMsg)
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

        private static DataflowTriggerKind GuessTriggerKind(EventSourceType eventSource)
        {
            return eventSource switch
            {
                EventSourceType.Schedule => DataflowTriggerKind.Schedule,
                EventSourceType.Http => DataflowTriggerKind.Http,
                _ => DataflowTriggerKind.Form,
            };
        }
    }
}
