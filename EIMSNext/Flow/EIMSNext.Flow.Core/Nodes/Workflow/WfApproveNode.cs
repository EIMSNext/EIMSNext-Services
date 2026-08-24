using System.Dynamic;

using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Service.Entities;
using EIMSNext.Flow.Core.Interfaces;

using HKH.Common;
using HKH.Mef2.Integration;

using MongoDB.Driver;

using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Nodes
{
    public class WfApproveNode : WfNodeAsyncBase<WfApproveNode>
    {
        public WfApproveNode(IResolver resolver) : base(resolver)
        {
        }

        public override async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
        {
            var dataContext = GetDataContext(context);
            var meta = Metadata!;

            if (context.ExecutionPointer.EventPublished)
            {
                var result = ApproveResult.Wait;

                var actResult = (ActivityResult)context.ExecutionPointer.EventData;
                var approveData = WfApproveData.FromExpando((ExpandoObject)actResult.Data);

                switch (approveData.Action)
                {
                    case ApproveAction.Approve:
                        {
                            //读取待办， 有待办的才有权限审批
                            var task = TaskRepository.Find(x => x.DataId == dataContext.DataId && x.ApproveNodeId == meta.Id && x.EmployeeId == approveData.WorkerId).FirstOrDefault();
                            if (task != null)
                            {
                                using (var scope = TaskRepository.NewTransactionScope())
                                {
                                    //写入审批记录
                                    AddTaskLog(context.Workflow, task, dataContext, Metadata!, approveData, scope.SessionHandle);

                                    if (meta.WfNodeSetting!.ApproveSetting!.ApprovalMode == WfApprovalMode.CounterSign)
                                    {
                                        //删除当前用户待办
                                        TaskRepository.Delete(task.Id, scope.SessionHandle);

                                        //会签时，所有人通过，才为审批通过
                                        var remainTaskCnt = TaskRepository.Find(x => x.DataId == dataContext.DataId && x.ApproveNodeId == meta.Id, scope.SessionHandle).CountDocuments();
                                        if (remainTaskCnt > 0)
                                        {
                                            //审批还没完成，重置事件继续等待
                                            result = ApproveResult.Wait;
                                        }
                                        else
                                        {
                                            var formData = GetFormData(dataContext.DataId);
                                            await RunDataflow(new DfRunParamter(dataContext.UserId, dataContext.AccessToken, formData, EventSourceType.Form, EventType.Approving, meta.Id, dataContext.WfStarter, dataContext.DfCascade, dataContext.EventIds));

                                            result = ApproveResult.Next;
                                        }
                                    }
                                    else
                                    {
                                        //或签时，任何一人通过，即为审批通过, 删除所有当前节点待办
                                        DeleteTasks(dataContext.CorpId, dataContext.DataId, meta.Id, scope.SessionHandle);

                                        var formData = GetFormData(dataContext.DataId);
                                        await RunDataflow(new DfRunParamter(dataContext.UserId, dataContext.AccessToken, formData, EventSourceType.Form, EventType.Approving, meta.Id, dataContext.WfStarter, dataContext.DfCascade, dataContext.EventIds));

                                        result = ApproveResult.Next;
                                    }

                                    scope.CommitTransaction();
                                }

                                CreateExecLog(context.Workflow, dataContext, meta, approveData);
                            }
                            else
                            {
                                CreateExecLog(context.Workflow, dataContext, meta, approveData, "没有审批权限");
                            }
                        }
                        break;
                    case ApproveAction.Reject:
                        {                            //读取待办， 有待办的才有权限审批
                            var task = TaskRepository.Find(x => x.DataId == dataContext.DataId && x.ApproveNodeId == meta.Id && x.EmployeeId == approveData.WorkerId).FirstOrDefault();
                            if (task != null)
                            {
                                using (var scope = TaskRepository.NewTransactionScope())
                                {
                                    UpdateWorkflowStatus(dataContext.CorpId, dataContext.DataId, FlowStatus.Rejected, scope.SessionHandle);

                                    //写入审批记录
                                    AddTaskLog(context.Workflow, task, dataContext, Metadata!, approveData, scope.SessionHandle);

                                    //删除待办记录
                                    DeleteTasks(dataContext.CorpId, dataContext.DataId, meta.Id, scope.SessionHandle);

                                    var formData = GetFormData(dataContext.DataId);
                                    await RunDataflow(new DfRunParamter(dataContext.UserId, dataContext.AccessToken, formData, EventSourceType.Form, EventType.Rejected, meta.Id, dataContext.WfStarter, dataContext.DfCascade, dataContext.EventIds));

                                    result = ApproveResult.Persist;

                                    scope.CommitTransaction();
                                }

                                //TODO：终止流程，将来可以改单据状态为草稿，允许重启流程
                                context.Workflow.Status = WorkflowStatus.Terminated;

                                CreateExecLog(context.Workflow, dataContext, meta, approveData);
                            }
                            else
                            {
                                CreateExecLog(context.Workflow, dataContext, meta, approveData, "没有审批权限");
                            }
                        }
                        break;
                    case ApproveAction.Return:
                        {
                            //退回到指定节点，审批轮次+1
                            //TODO:退回到指定节点，生成新待办

                            dataContext.Round += 1;
                            result = ApproveResult.Persist;
                        }
                        break;
                    default:
                        {
                            return ExecutionResult.Outcome(ApproveAction.None);
                        }
                }

                switch (result)
                {
                    case ApproveResult.Wait:
                        return RewaitActivity(context);
                    case ApproveResult.Next:
                        return ExecutionResult.Next();
                    default:
                        return ExecutionResult.Persist(context);
                }
            }
            else
            {
                if (ShouldAutoApprove(context.Workflow, dataContext, meta))
                {
                    var autoApproveData = new WfApproveData(
                        dataContext.CorpId!,
                        dataContext.UserId ?? string.Empty,
                        dataContext.WfStarter?.Id ?? string.Empty,
                        dataContext.WfStarter?.Value ?? string.Empty,
                        dataContext.WfStarter?.Label ?? "系统",
                        ApproveAction.AutoApprove,
                        "系统自动同意",
                        string.Empty,
                        Guid.NewGuid().ToString());

                    using (var scope = TaskRepository.NewTransactionScope())
                    {
                        AddTaskLog(context.Workflow, new Wf_Task { DataBrief = GetDataBrief(dataContext.FormId, dataContext.DataId) }, dataContext, Metadata!, autoApproveData, scope.SessionHandle);
                        scope.CommitTransaction();
                    }

                    CreateExecLog(context.Workflow, dataContext, meta, autoApproveData);
                    return ExecutionResult.Next();
                }

                //写入待办记录
                var tasks = await CreateTasks(context.Workflow, dataContext, meta, null);
                if (meta.WfNodeSetting?.ApproveSetting?.EnableCopyto == true)
                {
                    var ccEmpIds = await PopulateEmpIds(dataContext, meta.WfNodeSetting?.ApproveSetting?.CopytoCandidates);
                    await AddCCLogs(context.Workflow, dataContext, meta, ccEmpIds, null);
                }
                if (tasks.Count == 0)
                {
                    if (meta.WfNodeSetting?.ApproveSetting?.NoApproverSetting?.ActionType == NoApproverActionType.AutoSubmit)
                    {
                        var noApproverApproveData = new WfApproveData(
                            dataContext.CorpId!,
                            dataContext.UserId ?? string.Empty,
                            string.Empty,
                            string.Empty,
                            "系统",
                            ApproveAction.AutoApprove,
                            "找不到节点负责人，系统自动提交",
                            string.Empty,
                            Guid.NewGuid().ToString());

                        using (var scope = TaskRepository.NewTransactionScope())
                        {
                            AddTaskLog(context.Workflow, new Wf_Task { DataBrief = GetDataBrief(dataContext.FormId, dataContext.DataId) }, dataContext, Metadata!, noApproverApproveData, scope.SessionHandle);
                            scope.CommitTransaction();
                        }

                        CreateExecLog(context.Workflow, dataContext, meta, noApproverApproveData);
                        return ExecutionResult.Next();
                    }

                    var noApproverData = new WfApproveData(
                        dataContext.CorpId!,
                        dataContext.UserId ?? string.Empty,
                        string.Empty,
                        string.Empty,
                        "系统",
                        ApproveAction.None,
                        string.Empty,
                        string.Empty,
                        Guid.NewGuid().ToString());
                    CreateExecLog(context.Workflow, dataContext, meta, noApproverData, "找不到节点负责人");
                    throw new UnLogException("找不到节点负责人");
                }

                var def = GetWorkflowDefinition(context.Workflow);
                var notifyChannels = meta.WfNodeSetting?.ApproveSetting?.NotifyChannels ?? NotifyChannel.None;
                if (notifyChannels == NotifyChannel.None)
                {
                    notifyChannels = def?.Metadata?.WorkflowSetting?.NotifyChannels ?? NotifyChannel.None;
                }
                if (tasks.Count > 0 && notifyChannels != NotifyChannel.None)
                {
                    await Resolver.Resolve<IMessagePublisher>().PublishAsync(new NotifyDispatchTaskArgs
                    {
                        CorpId = dataContext.CorpId,
                        MessageType = MessageType.WfTaskNotify,
                        AppId = dataContext.AppId,
                        FormId = dataContext.FormId,
                        DataId = dataContext.DataId,
                        WfInstanceId = context.Workflow.Id,
                        ApproveNodeId = meta.Id,
                        EventStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    });
                }

                var activityKey = $"{context.Workflow.Id}_{dataContext.DataId}_{context.Step.ExternalId}";
                return ExecutionResult.WaitForActivity(activityKey, context.Workflow.Data, DateTime.Now);
            }
        }
    }

    enum ApproveResult
    {
        Wait,
        Next,
        Persist,
        Terminate
    }
}
