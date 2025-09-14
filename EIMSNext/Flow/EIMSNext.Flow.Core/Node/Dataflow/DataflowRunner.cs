
using EIMSNext.Core;
using EIMSNext.Core.Entity;
using EIMSNext.Core.Repository;
using EIMSNext.Entity;
using EIMSNext.Flow.Core.Interface;
using EIMSNext.Scripting;
using HKH.Mef2.Integration;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using WorkflowCore.Interface;

namespace EIMSNext.Flow.Core.Node
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

        public async Task<DfExecResult> RunAsync(FormData data, EventSourceType eventSource, EventType eventType, string wfNodeId, Operator? starter, CascadeMode cascade, string? eventIds)
        {
            var execResult = new DfExecResult();
            if (cascade == CascadeMode.Never || (cascade == CascadeMode.Specified && string.IsNullOrEmpty(eventIds)))
            {
                return execResult;
            }

            var dataflow = _resolver.Resolve<IRepository<Wf_Definition>>().Find(x => x.CorpId == data.CorpId && x.FlowType == FlowType.Dataflow
               && x.EventSource == eventSource && data.FormId.Equals(x.SourceId) && x.EventSetting != null && x.EventSetting.EventType.HasFlag(eventType)).FirstOrDefault();

            if (dataflow != null && (cascade == CascadeMode.All || eventIds!.Contains($",{dataflow.Id},")))
            {
                if (!IsMeet(dataflow, data))
                    return execResult;

                var ctx = new DfDataContext()
                {
                    CorpId = data.CorpId ?? "",
                    AppId = data.AppId,
                    FormId = data.FormId,
                    DataId = data.Id,
                    WfStarter = starter,
                    DfCascade = dataflow.EventSetting!.CascadeMode,
                    EventIds = dataflow.EventSetting.SpecifiedEvents
                };

                try
                {
                    var dfInst = await SyncWfRunner.RunWorkflowSync(dataflow.ExternalId, 1, ctx, "", CancellationToken.None, false);
                    var dfDataContext = dfInst.Data as DfDataContext;
                    execResult.DfInstance = dfInst;
                    execResult.Error = dfDataContext?.ErrMsg;
                }
                catch (Exception ex)
                {
                    execResult.Error = ex.Message;
                }
            }

            return execResult;
        }
    }
}
