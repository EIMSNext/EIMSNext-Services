using EIMSNext.Core;
using EIMSNext.Service.Entities;
using EIMSNext.Flow.Core.Interfaces;

using HKH.Mef2.Integration;

using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Nodes
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
                await RunDataflow(new DfRunParamter(dataContext.UserId, formData, EventSourceType.Form, EventType.Approved, "", dataContext.WfStarter, dataContext.DfCascade, dataContext.EventIds));

                scope.CommitTransaction();
            }

            return ExecutionResult.Next();
        }
    }
}
