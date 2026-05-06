using System.Dynamic;
using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Common.Extensions;
using EIMSNext.Core;
using EIMSNext.Core.Query;
using EIMSNext.Core.Repositories;
using EIMSNext.Flow.Core.Interfaces;
using EIMSNext.MongoDb;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;
using MongoDB.Bson;
using MongoDB.Driver;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Core
{
    public class WorkflowActionService : IWorkflowActionService
    {
        private readonly IResolver _resolver;
        private readonly IWfDefinitionService _definitionService;

        public WorkflowActionService(IResolver resolver)
        {
            _resolver = resolver;
            _definitionService = resolver.Resolve<IWfDefinitionService>();
        }

        public async Task<WorkflowActionResult> WithdrawAsync(WorkflowActionDataContext context, WorkflowInstance workflowInstance, Wf_Todo todo, string formName, string comment)
        {
            var definition = GetWorkflowDefinition(workflowInstance);
            var todoRepo = _resolver.GetRepository<Wf_Todo>();
            var approvalLogRepo = _resolver.GetRepository<Wf_ApprovalLog>();
            var workflowCollection = _resolver.Resolve<IMongoDbContex>().Database.GetCollection<WorkflowInstance>("Wf_WorkflowInstance");
            var subscriptionCollection = _resolver.Resolve<IMongoDbContex>().Database.GetCollection<EventSubscription>("Wf_Subscription");
            var dataContext = WfDataContext.FromExpando((ExpandoObject)workflowInstance.Data);
            dataContext.Round += 1;
            workflowInstance.Data = dataContext.ToExpando();
            workflowInstance.Status = WorkflowStatus.Suspended;
            workflowInstance.NextExecution = null;
            workflowInstance.CompleteTime = null;
            ResetWorkflowPointers(workflowInstance, definition);

            using (var scope = todoRepo.NewTransactionScope())
            {
                todoRepo.Delete(new DynamicFilter
                {
                    Rel = "and",
                    Items = [
                        new DynamicFilter { Field = "WfInstanceId", Op = FilterOp.Eq, Value = workflowInstance.Id },
                    ]
                }, scope.SessionHandle);

                workflowCollection.ReplaceOne(session: scope.SessionHandle, x => x.Id == workflowInstance.Id, workflowInstance);
                subscriptionCollection.DeleteMany(session: scope.SessionHandle, x => x.WorkflowId == workflowInstance.Id);

                approvalLogRepo.Insert(new Wf_ApprovalLog
                {
                    CorpId = context.CorpId,
                    AppId = todo.AppId,
                    FormId = todo.FormId,
                    DataId = todo.DataId,
                    FormName = formName,
                    WfVersion = workflowInstance.Version,
                    NodeId = todo?.ApproveNodeId ?? string.Empty,
                    NodeName = todo?.ApproveNodeName ?? "发起节点",
                    NodeType = WfNodeType.Start,
                    Round = dataContext.Round - 1,
                    Approver = context.CurrentEmployee,
                    Result = ApproveAction.Withdraw,
                    Comment = comment,
                    ApprovalTime = DateTime.UtcNow.ToTimeStampMs(),
                    DataBrief = todo?.DataBrief ?? [],
                }, scope.SessionHandle);

                scope.CommitTransaction();
            }

            return new WorkflowActionResult { WorkflowInstanceId = workflowInstance.Id };
        }

        public async Task<WorkflowActionResult> UrgeAsync(WorkflowActionDataContext context, WorkflowInstance workflowInstance, Wf_Todo todo, string dataId)
        {
            await _resolver.Resolve<IMessagePublisher>().PublishAsync(new NotifyDispatchTaskArgs
            {
                CorpId = context.CorpId,
                MessageType = MessageType.WfUrgeNotify,
                AppId = todo?.AppId,
                FormId = todo?.FormId,
                DataId = dataId,
                WfInstanceId = workflowInstance.Id,
                ApproveNodeId = todo?.ApproveNodeId,
            });

            return new WorkflowActionResult { WorkflowInstanceId = workflowInstance.Id };
        }

        public WorkflowActionStatusResult GetActionStatus(string currentEmployeeId, Wf_Todo? todo, Wf_Definition? definition)
        {
            if (todo == null)
            {
                return new WorkflowActionStatusResult();
            }

            var isStarter = todo.Starter?.Id == currentEmployeeId;
            var withdrawRule = definition?.Metadata?.WorkflowSetting?.WithdrawRule ?? WorkflowWithdrawRule.Disabled;
            var firstApproveNodeId = definition?.Metadata?.Steps?.FirstOrDefault(x => x.NodeType == WfNodeType.Approve)?.Id;

            return new WorkflowActionStatusResult
            {
                CanUrge = isStarter && definition?.Metadata?.WorkflowSetting?.AllowUrge == true,
                CanWithdraw = isStarter
                    && withdrawRule != WorkflowWithdrawRule.Disabled
                    && (withdrawRule == WorkflowWithdrawRule.AllNodes || firstApproveNodeId == todo?.ApproveNodeId)
            };
        }

        private Wf_Definition? GetWorkflowDefinition(WorkflowInstance wfInst)
        {
            return _definitionService.Find(x => x.ExternalId == wfInst.WorkflowDefinitionId && x.Version == wfInst.Version).FirstOrDefault();
        }

        private static void ResetWorkflowPointers(WorkflowInstance wfInst, Wf_Definition? definition)
        {
            var startStep = definition?.Metadata?.Steps?.FirstOrDefault(x => x.NodeType == WfNodeType.Start);
            if (startStep == null)
            {
                throw new InvalidOperationException("流程定义缺少开始节点，无法重置流程实例");
            }

            wfInst.ExecutionPointers.Clear();
            wfInst.ExecutionPointers.Add(new ExecutionPointer
            {
                Id = ObjectId.GenerateNewId().ToString(),
                StepId = 0,
                StepName = startStep.Name,
                Active = true,
                Status = PointerStatus.Pending,
                StartTime = null,
                EndTime = null,
                SleepUntil = null,
                PersistenceData = null,
                EventName = null,
                EventKey = null,
                EventPublished = false,
                EventData = null,
                RetryCount = 0,
                Children = [],
                ContextItem = null,
                PredecessorId = null,
                Outcome = null,
                Scope = []
            });
        }
    }
}
