using EIMSNext.Core;
using EIMSNext.Entity;
using HKH.Mef2.Integration;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Node
{
    public class WfStartNode : WfNodeAsyncBase<WfStartNode>
    {
        public WfStartNode(IResolver resolver) : base(resolver)
        {
        }

        public override async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
        {
            var dataContext = GetDataContext(context);

            var approveData = new WfApproveData(dataContext.WfStarter?.CorpId!, dataContext.WfStarter!.UserId ?? "", dataContext.WfStarter.EmpId, dataContext.WfStarter.EmpName,
                ApproveAction.Approve, string.Empty, string.Empty, context.Workflow.Id);

            using (var scope = FormDataRepository.NewTransactionScope())
            {
                UpdateWorkflowStatus(dataContext.CorpId, dataContext.DataId, FlowStatus.Approving, scope.SessionHandle);
                AddApprovalLog(context.Workflow, new Wf_Todo(), dataContext, Metadata!, approveData, scope.SessionHandle);

                var formData = GetFormData(dataContext.DataId);
                await RunDataflow(formData, EventSourceType.Form, EventType.Submitted, "", dataContext.WfStarter, dataContext.DfCascade, dataContext.EventIds);

                scope.CommitTransaction();
            }

            CreateExecLog(context.Workflow, dataContext, Metadata!, approveData);

            return ExecutionResult.Next();
        }
    }
}
