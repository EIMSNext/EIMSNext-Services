using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Entities;
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

            await TransactionScope.ExecuteWithRetryAsync(FormDataRepository.DbContext, async () =>
            {
                await UpdateWorkflowStatus(dataContext.CorpId, dataContext.DataId, FlowStatus.Approved);

                var formData = GetFormData(dataContext.DataId);
                await RunEventFlow(new EfRunParameter(dataContext.UserId, dataContext.AccessToken, formData, EventSourceType.Form, EventType.Approved, "", dataContext.WfStarter, dataContext.EfCascade, dataContext.EventIds)
                    .WithExecutionId($"{context.Workflow.Id}:end:{dataContext.Round}:approved"));

            }).ConfigureAwait(false);

            return ExecutionResult.Next();
        }
    }
}
