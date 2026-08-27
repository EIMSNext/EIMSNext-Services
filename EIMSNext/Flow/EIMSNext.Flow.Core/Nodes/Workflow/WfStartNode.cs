using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Entities;
using EIMSNext.Flow.Core.Interfaces;

using HKH.Mef2.Integration;

using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Nodes
{
    public class WfStartNode : WfNodeAsyncBase<WfStartNode>
    {
        public WfStartNode(IResolver resolver) : base(resolver)
        {
        }

        public override async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
        {
            var dataContext = GetDataContext(context);

            var approveData = new WfApproveData(dataContext.CorpId!, dataContext.UserId ?? "", dataContext.WfStarter!.Id, dataContext.WfStarter.Value, dataContext.WfStarter.Label,
                ApproveAction.Approve, string.Empty, string.Empty, context.Workflow.Id);

            using (var scope = FormDataRepository.NewTransactionScope())
            {
                UpdateWorkflowStatus(dataContext.CorpId, dataContext.DataId, FlowStatus.Approving, scope.SessionHandle);
                AddTaskLog(context.Workflow, new Wf_Task(), dataContext, Metadata!, approveData, scope.SessionHandle);

                var formData = GetFormData(dataContext.DataId);
                await RunEventFlow(new EfRunParameter(dataContext.UserId ?? "", dataContext.AccessToken, formData, EventSourceType.Form, EventType.Submitted, "", dataContext.WfStarter, dataContext.EfCascade, dataContext.EventIds));

                scope.CommitTransaction();
            }

            CreateExecLog(context.Workflow, dataContext, Metadata!, approveData);

            return ExecutionResult.Next();
        }
    }
}
