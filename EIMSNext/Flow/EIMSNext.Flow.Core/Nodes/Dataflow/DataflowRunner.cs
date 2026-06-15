
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

            var dataflow = _resolver.Resolve<IRepository<Wf_Definition>>().Find(x => x.CorpId == paramter.Data.CorpId && x.FlowType == FlowType.Dataflow
               && x.EventSource == paramter.EventSource && paramter.Data.FormId.Equals(x.SourceId) && x.EventSetting != null && x.EventSetting.EventType.HasFlag(paramter.EventType)).FirstOrDefault();

            if (dataflow != null && (paramter.Cascade == CascadeMode.NotSet || paramter.Cascade == CascadeMode.All || (!string.IsNullOrEmpty(paramter.EventIds) && paramter.EventIds.Contains($",{dataflow.Id},"))))
            {
                if (!IsMeet(dataflow, paramter.Data))
                    return execResult;

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
