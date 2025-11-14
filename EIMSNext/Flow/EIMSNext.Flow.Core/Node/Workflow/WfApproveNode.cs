using System.Dynamic;
using EIMSNext.Core;
using EIMSNext.Entity;
using HKH.Mef2.Integration;
using MongoDB.Driver;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Node
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
                            var todo = TodoRepository.Find(x => x.DataId == dataContext.DataId && x.ApproveNodeId == meta.Id && x.EmployeeId == approveData.WorkerId).FirstOrDefault();
                            if (todo != null)
                            {

                                CreateExecLog(context.Workflow, dataContext, meta, approveData);

                                using (var scope = TodoRepository.NewTransactionScope())
                                {
                                    //写入审批记录
                                    AddApprovalLog(context.Workflow, todo, dataContext, Metadata!, approveData, scope.SessionHandle);

                                    if (meta.WfNodeSetting!.ApproveSetting!.ApprovalMode == WfApprovalMode.CounterSign)
                                    {
                                        //删除当前用户待办
                                        TodoRepository.Delete(todo.Id, scope.SessionHandle);

                                        //会签时，所有人通过，才为审批通过
                                        var remainTodoCnt = TodoRepository.Find(x => x.DataId == dataContext.DataId && x.ApproveNodeId == meta.Id, scope.SessionHandle).CountDocuments();
                                        if (remainTodoCnt > 0)
                                        {
                                            //审批还没完成，重置事件继续等待
                                            result = ApproveResult.Wait;
                                        }
                                        else
                                        {
                                            var formData = GetFormData(dataContext.DataId);
                                            await RunDataflow(formData, EventSourceType.Form, EventType.Approving, meta.Id, dataContext.WfStarter, dataContext.DfCascade, dataContext.EventIds);

                                            result = ApproveResult.Next;
                                        }
                                    }
                                    else
                                    {
                                        //或签时，任何一人通过，即为审批通过, 删除所有当前节点待办
                                        DeleteTodos(dataContext.CorpId, dataContext.DataId, meta.Id, scope.SessionHandle);

                                        var formData = GetFormData(dataContext.DataId);
                                        await RunDataflow(formData, EventSourceType.Form, EventType.Approving, meta.Id, dataContext.WfStarter, dataContext.DfCascade, dataContext.EventIds);

                                        result = ApproveResult.Next;
                                    }

                                    scope.CommitTransaction();
                                }
                            }
                        }
                        break;
                    case ApproveAction.Reject:
                        {                            //读取待办， 有待办的才有权限审批
                            var todo = TodoRepository.Find(x => x.DataId == dataContext.DataId && x.ApproveNodeId == meta.Id && x.EmployeeId == approveData.WorkerId).FirstOrDefault();
                            if (todo != null)
                            {
                                CreateExecLog(context.Workflow, dataContext, meta, approveData);

                                using (var scope = TodoRepository.NewTransactionScope())
                                {
                                    UpdateWorkflowStatus(dataContext.CorpId, dataContext.DataId, FlowStatus.Rejected, scope.SessionHandle);

                                    //写入审批记录
                                    AddApprovalLog(context.Workflow, todo, dataContext, Metadata!, approveData, scope.SessionHandle);

                                    //删除待办记录
                                    DeleteTodos(dataContext.CorpId, dataContext.DataId, meta.Id, scope.SessionHandle);

                                    var formData = GetFormData(dataContext.DataId);
                                    await RunDataflow(formData, EventSourceType.Form, EventType.Rejected, meta.Id, dataContext.WfStarter, dataContext.DfCascade, dataContext.EventIds);

                                    result = ApproveResult.Persist;

                                    scope.CommitTransaction();
                                }

                                //TODO：终止流程，将来可以改单据状态为草稿，允许重启流程
                                context.Workflow.Status = WorkflowStatus.Terminated;
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
                //写入待办记录
                CreateTodos(context.Workflow, dataContext, meta, null);

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
