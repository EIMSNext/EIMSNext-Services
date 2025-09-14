using HKH.Mef2.Integration;

using EIMSNext.Core;
using EIMSNext.Entity;

using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Node
{
    public class WfEndNode : WfNodeAsyncBase<WfEndNode>
    {
        public WfEndNode(IResolver resolver) : base(resolver)
        {
        }

        public override async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
        {
            var dataContext = GetDataContext(context);

            using (var scope = FormDataRepository.NewTransactionScope())
            {
                UpdateWorkflowStatus(dataContext.CorpId, dataContext.DataId, FlowStatus.Approved, scope.SessionHandle);

                var formData = GetFormData(dataContext.DataId);
                await RunDataflow(formData, EventSourceType.Form, EventType.Approved, "", dataContext.WfStarter, dataContext.DfCascade, dataContext.EventIds);

                scope.CommitTransaction();
            }

            return ExecutionResult.Next();
        }
    }
}
