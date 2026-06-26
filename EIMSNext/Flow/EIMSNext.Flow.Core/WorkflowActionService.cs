using System.Dynamic;
using System.Linq;
using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Common.Extensions;
using EIMSNext.Core;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Query;
using EIMSNext.Core.Repositories;
using EIMSNext.Flow.Core.Interfaces;
using EIMSNext.MongoDb;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using EIMSNext.Scripting;
using HKH.Mef2.Integration;
using MongoDB.Bson;
using MongoDB.Driver;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Core
{
    public class WorkflowActionService : IWorkflowActionService
    {
        private readonly IResolver _resolver;
        private readonly IWfDefinitionService _definitionService;
        private readonly IRepository<Wf_Todo> _todoRepo;
        private readonly IRepository<Wf_ApprovalLog> _approvalLogRepo;
        private readonly IRepository<FormDef> _formDefRepo;
        private readonly IRepository<FormData> _formDataRepo;
        private readonly IRepository<Employee> _employeeRepo;
        private readonly IRepository<EmployeeDepartment> _employeeDepartmentRepo;
        private readonly IRepository<Department> _departmentRepo;
        private readonly IMongoCollection<WorkflowInstance> _workflowCollection;
        private readonly IMongoCollection<EventSubscription> _subscriptionCollection;
        private readonly IWorkflowHost _workflowHost;

        public WorkflowActionService(IResolver resolver)
        {
            _resolver = resolver;
            _definitionService = resolver.Resolve<IWfDefinitionService>();
            _todoRepo = resolver.GetRepository<Wf_Todo>();
            _approvalLogRepo = resolver.GetRepository<Wf_ApprovalLog>();
            _formDefRepo = resolver.GetRepository<FormDef>();
            _formDataRepo = resolver.GetRepository<FormData>();
            _employeeRepo = resolver.GetRepository<Employee>();
            _employeeDepartmentRepo = resolver.GetRepository<EmployeeDepartment>();
            _departmentRepo = resolver.GetRepository<Department>();
            _workflowHost = resolver.Resolve<IWorkflowHost>();
            var db = resolver.Resolve<IMongoDbContex>().Database;
            _workflowCollection = db.GetCollection<WorkflowInstance>("Wf_WorkflowInstance");
            _subscriptionCollection = db.GetCollection<EventSubscription>("Wf_Subscription");
        }

        public async Task<WorkflowActionResult> WithdrawAsync(WorkflowActionDataContext context, WorkflowInstance workflowInstance, Wf_Todo todo, string formName, string comment)
        {
            var definition = GetWorkflowDefinition(workflowInstance);
            var dataContext = WfDataContext.FromExpando((ExpandoObject)workflowInstance.Data);
            dataContext.Round += 1;
            workflowInstance.Data = dataContext.ToExpando();
            workflowInstance.Status = WorkflowStatus.Suspended;
            workflowInstance.NextExecution = null;
            workflowInstance.CompleteTime = null;
            ResetWorkflowPointers(workflowInstance, definition);

            using var scope = _todoRepo.NewTransactionScope();
            _todoRepo.Delete(new DynamicFilter
            {
                Rel = "and",
                Items = [new DynamicFilter { Field = "WfInstanceId", Op = FilterOp.Eq, Value = workflowInstance.Id }]
            }, scope.SessionHandle);

            _workflowCollection.ReplaceOne(scope.SessionHandle, x => x.Id == workflowInstance.Id, workflowInstance);
            _subscriptionCollection.DeleteMany(scope.SessionHandle, x => x.WorkflowId == workflowInstance.Id);

            _approvalLogRepo.Insert(CreateApprovalLog(context, workflowInstance, todo, WfNodeType.Start, todo.ApproveNodeId, todo.ApproveNodeName, ApproveAction.Withdraw, comment, dataContext.Round - 1), scope.SessionHandle);
            scope.CommitTransaction();

            return new WorkflowActionResult { WorkflowInstanceId = workflowInstance.Id };
        }

        public async Task<WorkflowActionResult> UrgeAsync(WorkflowActionDataContext context, WorkflowInstance workflowInstance, Wf_Todo todo, string dataId)
        {
            await _resolver.Resolve<IMessagePublisher>().PublishAsync(new NotifyDispatchTaskArgs
            {
                CorpId = context.CorpId,
                MessageType = MessageType.WfUrgeNotify,
                AppId = todo.AppId,
                FormId = todo.FormId,
                DataId = dataId,
                WfInstanceId = workflowInstance.Id,
                ApproveNodeId = todo.ApproveNodeId,
            });

            return new WorkflowActionResult { WorkflowInstanceId = workflowInstance.Id };
        }

        public async Task<WorkflowActionResult> TransferAsync(WorkflowActionDataContext context, WorkflowInstance workflowInstance, Wf_Todo todo, string targetEmployeeId, string comment)
        {
            if (string.IsNullOrWhiteSpace(targetEmployeeId))
            {
                throw new InvalidOperationException("转交目标不能为空");
            }

            if (targetEmployeeId == context.CurrentEmployeeId)
            {
                throw new InvalidOperationException("转交目标不能是本人");
            }

            await ValidateNodeActionEnabledAsync(workflowInstance, todo, NodeActionType.Transfer);
            await ValidateTargetEmployeeAsync(workflowInstance, todo, NodeActionType.Transfer, targetEmployeeId);

            using var scope = _todoRepo.NewTransactionScope();
            _todoRepo.Update(todo.Id,
                Builders<Wf_Todo>.Update
                    .Set(x => x.EmployeeId, targetEmployeeId)
                    .Set(x => x.UpdateTime, DateTime.UtcNow.ToTimeStampMs()),
                session: scope.SessionHandle);

            var dataContext = WfDataContext.FromExpando((ExpandoObject)workflowInstance.Data);
            _approvalLogRepo.Insert(CreateApprovalLog(context, workflowInstance, todo, WfNodeType.Approve, todo.ApproveNodeId, todo.ApproveNodeName, ApproveAction.Transfer, comment, dataContext.Round), scope.SessionHandle);
            scope.CommitTransaction();

            return new WorkflowActionResult { WorkflowInstanceId = workflowInstance.Id };
        }

        public async Task<WorkflowActionResult> AddSignAsync(WorkflowActionDataContext context, WorkflowInstance workflowInstance, Wf_Todo todo, string targetEmployeeId, string comment)
        {
            if (string.IsNullOrWhiteSpace(targetEmployeeId))
            {
                throw new InvalidOperationException("加签目标不能为空");
            }

            await ValidateNodeActionEnabledAsync(workflowInstance, todo, NodeActionType.AddSign);
            await ValidateTargetEmployeeAsync(workflowInstance, todo, NodeActionType.AddSign, targetEmployeeId);

            var dataContext = WfDataContext.FromExpando((ExpandoObject)workflowInstance.Data);
            using var scope = _todoRepo.NewTransactionScope();
            _approvalLogRepo.Insert(CreateApprovalLog(context, workflowInstance, todo, WfNodeType.Approve, todo.ApproveNodeId, todo.ApproveNodeName, ApproveAction.AddSignAfter, comment, dataContext.Round), scope.SessionHandle);
            _todoRepo.Delete(todo.Id, scope.SessionHandle);

            var newTodo = CloneTodo(todo, targetEmployeeId);
            _todoRepo.Insert(newTodo, scope.SessionHandle);
            scope.CommitTransaction();

            return new WorkflowActionResult { WorkflowInstanceId = workflowInstance.Id };
        }

        public async Task<WorkflowActionResult> ChangeApproverAsync(WorkflowActionDataContext context, WorkflowInstance workflowInstance, Wf_Todo todo, string targetEmployeeId, string comment)
        {
            if (string.IsNullOrWhiteSpace(targetEmployeeId))
            {
                throw new InvalidOperationException("审批人不能为空");
            }

            if (targetEmployeeId == todo.EmployeeId)
            {
                throw new InvalidOperationException("当前节点审批人未发生变化");
            }

            var employee = await _employeeRepo.GetAsync(targetEmployeeId);
            if (employee == null || employee.CorpId != context.CorpId)
            {
                throw new InvalidOperationException("目标审批人不存在");
            }

            using var scope = _todoRepo.NewTransactionScope();
            _todoRepo.Update(todo.Id,
                Builders<Wf_Todo>.Update
                    .Set(x => x.EmployeeId, targetEmployeeId)
                    .Set(x => x.UpdateTime, DateTime.UtcNow.ToTimeStampMs())
                    .Set(x => x.UpdateBy, context.CurrentEmployee),
                session: scope.SessionHandle);

            var dataContext = WfDataContext.FromExpando((ExpandoObject)workflowInstance.Data);
            _approvalLogRepo.Insert(CreateApprovalLog(context, workflowInstance, todo, WfNodeType.Approve, todo.ApproveNodeId, todo.ApproveNodeName, ApproveAction.ChangeApprover, comment, dataContext.Round), scope.SessionHandle);
            scope.CommitTransaction();

            return new WorkflowActionResult { WorkflowInstanceId = workflowInstance.Id };
        }

        public async Task<List<ReturnTargetNodeResult>> GetReturnNodesAsync(WorkflowActionDataContext context, WorkflowInstance workflowInstance, Wf_Todo todo)
        {
            var dataContext = WfDataContext.FromExpando((ExpandoObject)workflowInstance.Data);
            await EnsureStartApprovalLogAsync(workflowInstance, dataContext);

            var trail = GetReturnTrail(workflowInstance, todo, dataContext.Round);
            return trail
                .Where(x => x.NodeId != todo.ApproveNodeId)
                .Select(x => new ReturnTargetNodeResult { NodeId = x.NodeId, NodeName = x.NodeName, Round = x.Round })
                .DistinctBy(x => x.NodeId)
                .ToList();
        }

        public async Task<WorkflowActionResult> ReturnAsync(WorkflowActionDataContext context, WorkflowInstance workflowInstance, Wf_Todo todo, string targetNodeId, string comment)
        {
            return await ReturnInternalAsync(context, workflowInstance, todo, targetNodeId, comment, ApproveAction.Return);
        }

        private async Task<WorkflowActionResult> ReturnInternalAsync(WorkflowActionDataContext context, WorkflowInstance workflowInstance, Wf_Todo todo, string targetNodeId, string comment, ApproveAction action)
        {
            if (string.IsNullOrWhiteSpace(targetNodeId))
            {
                throw new InvalidOperationException("回退节点不能为空");
            }

            if (action == ApproveAction.Return)
            {
                await ValidateNodeActionEnabledAsync(workflowInstance, todo, NodeActionType.Return);
            }

            var dataContext = WfDataContext.FromExpando((ExpandoObject)workflowInstance.Data);
            await EnsureStartApprovalLogAsync(workflowInstance, dataContext);

            var trail = GetReturnTrail(workflowInstance, todo, dataContext.Round);
            var target = trail.FirstOrDefault(x => x.NodeId == targetNodeId);
            if (target == null)
            {
                throw new InvalidOperationException("目标节点不在可回退范围内");
            }

            var definition = GetWorkflowDefinition(workflowInstance) ?? throw new InvalidOperationException("流程定义不存在");
            dataContext.Round += 1;
            workflowInstance.Data = dataContext.ToExpando();
            workflowInstance.NextExecution = null;
            workflowInstance.CompleteTime = null;

            using var scope = _todoRepo.NewTransactionScope();
            _todoRepo.Delete(new DynamicFilter
            {
                Rel = "and",
                Items = [
                    new DynamicFilter { Field = "WfInstanceId", Op = FilterOp.Eq, Value = workflowInstance.Id },
                ]
            }, scope.SessionHandle);

            if (target.NodeType == WfNodeType.Start)
            {
                workflowInstance.Status = WorkflowStatus.Suspended;
                ResetWorkflowPointers(workflowInstance, definition, target.NodeId);
                _workflowCollection.ReplaceOne(scope.SessionHandle, x => x.Id == workflowInstance.Id, workflowInstance);
                _subscriptionCollection.DeleteMany(scope.SessionHandle, x => x.WorkflowId == workflowInstance.Id);
                UpdateFormStatus(todo.DataId, FlowStatus.Draft, scope.SessionHandle);
            }
            else
            {
                workflowInstance.Status = WorkflowStatus.Runnable;
                ResetWorkflowPointers(workflowInstance, definition, target.NodeId);
                _workflowCollection.ReplaceOne(scope.SessionHandle, x => x.Id == workflowInstance.Id, workflowInstance);
                _subscriptionCollection.DeleteMany(scope.SessionHandle, x => x.WorkflowId == workflowInstance.Id);
                UpdateFormStatus(todo.DataId, FlowStatus.Approving, scope.SessionHandle);
            }

            _approvalLogRepo.Insert(CreateApprovalLog(context, workflowInstance, todo, WfNodeType.Approve, todo.ApproveNodeId, todo.ApproveNodeName, action, comment, dataContext.Round - 1), scope.SessionHandle);
            scope.CommitTransaction();

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
                    && (withdrawRule == WorkflowWithdrawRule.AllNodes || firstApproveNodeId == todo.ApproveNodeId)
            };
        }

        public Task ValidateSubmitConditionAsync(WorkflowInstance workflowInstance, Wf_Todo todo)
        {
            var definition = GetWorkflowDefinition(workflowInstance) ?? throw new InvalidOperationException("流程定义不存在");
            var step = definition.Metadata?.Steps?.FirstOrDefault(x => x.Id == todo.ApproveNodeId);
            var submitCondition = step?.WfNodeSetting?.ApproveSetting?.SubmitCondition;
            if (submitCondition?.Enabled != true || string.IsNullOrWhiteSpace(submitCondition.Expression))
            {
                return Task.CompletedTask;
            }

            var formData = _formDataRepo.Get(todo.DataId) ?? throw new InvalidOperationException("审批数据不存在");
            var scriptData = formData.Data;
            scriptData.TryAdd(EIMSNext.Common.Fields.CreateBy, formData.CreateBy);
            scriptData.TryAdd(WfConsts.MatchedResult, false);

            var wrapData = new ExpandoObject();
            wrapData.TryAdd($"f_{formData.FormId}", scriptData);

            var result = _resolver.Resolve<IScriptEngine>().Evaluate(submitCondition.Expression, new Dictionary<string, object>
            {
                ["data"] = wrapData,
            });

            if (!Convert.ToBoolean(result.Value))
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(submitCondition.PromptText)
                    ? "当前数据不满足提交条件"
                    : submitCondition.PromptText);
            }

            return Task.CompletedTask;
        }

        public Task ValidateNodeActionEnabledAsync(WorkflowInstance workflowInstance, Wf_Todo todo, NodeActionType actionType)
        {
            var definition = GetWorkflowDefinition(workflowInstance) ?? throw new InvalidOperationException("流程定义不存在");
            var step = definition.Metadata?.Steps?.FirstOrDefault(x => x.Id == todo.ApproveNodeId);
            var action = step?.WfNodeSetting?.ApproveSetting?.NodeActions?.FirstOrDefault(x => x.ActionType == actionType && x.Enabled);
            if (action == null)
            {
                throw new InvalidOperationException("当前节点未启用该操作");
            }

            return Task.CompletedTask;
        }

        public async Task<WorkflowActionResult> HandleExpiredTodoAsync(WorkflowInstance workflowInstance, Wf_Todo todo)
        {
            var definition = GetWorkflowDefinition(workflowInstance) ?? throw new InvalidOperationException("流程定义不存在");
            var step = definition.Metadata?.Steps?.FirstOrDefault(x => x.Id == todo.ApproveNodeId) ?? throw new InvalidOperationException("审批节点不存在");
            var expireSetting = step.WfNodeSetting?.ApproveSetting?.ExpireSetting ?? throw new InvalidOperationException("审批节点未配置超时动作");

            if (expireSetting.TimeValue <= 0)
            {
                return new WorkflowActionResult { WorkflowInstanceId = workflowInstance.Id };
            }

            var context = new WorkflowActionDataContext
            {
                CorpId = todo.CorpId ?? string.Empty,
                CurrentEmployeeId = "system",
                CurrentEmployee = _resolver.GetServiceContext().Operator ?? new Operator("system", $"wf_{workflowInstance.Id}", "System")
            };

            return expireSetting.ActionType switch
            {
                WfExpireActionType.AutoApprove => await SubmitExpiredActivityAsync(workflowInstance, todo, ApproveAction.Approve, "审批超时，系统自动通过"),
                WfExpireActionType.AutoReject => await SubmitExpiredActivityAsync(workflowInstance, todo, ApproveAction.Reject, "审批超时，系统自动驳回"),
                WfExpireActionType.AutoTransfer => await HandleExpiredTransferAsync(context, workflowInstance, todo, expireSetting),
                WfExpireActionType.AutoReturn => await HandleExpiredReturnAsync(context, workflowInstance, todo, expireSetting),
                _ => new WorkflowActionResult { WorkflowInstanceId = workflowInstance.Id }
            };
        }

        private async Task ValidateTargetEmployeeAsync(WorkflowInstance workflowInstance, Wf_Todo todo, NodeActionType actionType, string targetEmployeeId)
        {
            var definition = GetWorkflowDefinition(workflowInstance) ?? throw new InvalidOperationException("流程定义不存在");
            var step = definition.Metadata?.Steps?.FirstOrDefault(x => x.Id == todo.ApproveNodeId);
            var action = step?.WfNodeSetting?.ApproveSetting?.NodeActions?.FirstOrDefault(x => x.ActionType == actionType && x.Enabled)
                ?? throw new InvalidOperationException("当前节点未启用该操作");
            var candidateIds = await PopulateEmpIds(todo.DataId, action.Candidates);
            if (!candidateIds.Contains(targetEmployeeId))
            {
                throw new InvalidOperationException("目标人员不在候选范围内");
            }
        }

        private async Task<List<string>> PopulateEmpIds(string dataId, IList<ApprovalCandidate>? candidates)
        {
            var dataContext = WfDataContext.FromExpando((ExpandoObject)GetWorkflowInstanceData(dataId));
            return (await PopulateEmpIds(dataContext, candidates)).ToList();
        }

        private async Task<WorkflowActionResult> SubmitExpiredActivityAsync(WorkflowInstance workflowInstance, Wf_Todo todo, ApproveAction action, string comment)
        {
            var activity = await _workflowHost.GetPendingActivity($"{workflowInstance.Id}_{todo.DataId}_{todo.ApproveNodeId}", todo.EmployeeId);
            if (activity == null)
            {
                throw new InvalidOperationException("审批超时自动处理失败：当前节点活动不存在");
            }

            var approveData = new WfApproveData(
                todo.CorpId ?? string.Empty,
                string.Empty,
                todo.EmployeeId,
                string.Empty,
                "系统",
                action,
                comment,
                string.Empty,
                Guid.NewGuid().ToString());

            await _workflowHost.SubmitActivitySuccess(activity.Token, approveData.ToExpando());
            return new WorkflowActionResult { WorkflowInstanceId = workflowInstance.Id };
        }

        private async Task<WorkflowActionResult> HandleExpiredTransferAsync(WorkflowActionDataContext context, WorkflowInstance workflowInstance, Wf_Todo todo, ExpireSetting expireSetting)
        {
            var targetEmployeeIds = (await PopulateEmpIds(todo.DataId, expireSetting.TransferSetting?.Candidates))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();
            if (targetEmployeeIds.Count == 0)
            {
                throw new InvalidOperationException("审批超时自动转交失败：未找到目标审批人");
            }

            var sourceTodos = _todoRepo.Find(x => x.WfInstanceId == workflowInstance.Id && x.ApproveNodeId == todo.ApproveNodeId).ToList();
            if (sourceTodos.Count == 0)
            {
                return new WorkflowActionResult { WorkflowInstanceId = workflowInstance.Id };
            }

            using var scope = _todoRepo.NewTransactionScope();
            var dataContext = WfDataContext.FromExpando((ExpandoObject)workflowInstance.Data);
            var now = DateTime.UtcNow.ToTimeStampMs();
            var replacementTodos = sourceTodos
                .Select((sourceTodo, index) => CloneTodo(sourceTodo, targetEmployeeIds[Math.Min(index, targetEmployeeIds.Count - 1)]))
                .ToList();
            replacementTodos.ForEach(x => x.UpdateTime = now);

            _todoRepo.Delete(new DynamicFilter
            {
                Rel = "and",
                Items =
                [
                    new DynamicFilter { Field = "WfInstanceId", Op = FilterOp.Eq, Value = workflowInstance.Id },
                    new DynamicFilter { Field = "ApproveNodeId", Op = FilterOp.Eq, Value = todo.ApproveNodeId }
                ]
            }, scope.SessionHandle);
            _todoRepo.Insert(replacementTodos, scope.SessionHandle);

            foreach (var sourceTodo in sourceTodos)
            {
                _approvalLogRepo.Insert(CreateApprovalLog(context, workflowInstance, sourceTodo, WfNodeType.Approve, sourceTodo.ApproveNodeId, sourceTodo.ApproveNodeName, ApproveAction.AutoTransfer, "审批超时，系统自动转交", dataContext.Round), scope.SessionHandle);
            }
            scope.CommitTransaction();

            return new WorkflowActionResult { WorkflowInstanceId = workflowInstance.Id };
        }

        private async Task<WorkflowActionResult> HandleExpiredReturnAsync(WorkflowActionDataContext context, WorkflowInstance workflowInstance, Wf_Todo todo, ExpireSetting expireSetting)
        {
            var targetNodeId = await ResolveExpireReturnTargetNodeIdAsync(workflowInstance, todo, expireSetting.ReturnSetting);
            return await ReturnInternalAsync(context, workflowInstance, todo, targetNodeId, "审批超时，系统自动回退", ApproveAction.AutoReturn);
        }

        private ExpandoObject GetWorkflowInstanceData(string dataId)
        {
            var wfInst = _workflowCollection.Find(x => x.Reference == dataId).SortByDescending(x => x.CreateTime).FirstOrDefault();
            return (ExpandoObject)(wfInst?.Data ?? new ExpandoObject());
        }

        private async Task<IEnumerable<string>> PopulateEmpIds(WfDataContext dataContext, IList<ApprovalCandidate>? candidates)
        {
            var resolver = new WorkflowCandidateResolver(_employeeRepo, _employeeDepartmentRepo, _departmentRepo, _formDefRepo, _formDataRepo);
            return await resolver.ResolveEmployeeIdsAsync(dataContext, candidates);
        }

        private async Task EnsureStartApprovalLogAsync(WorkflowInstance workflowInstance, WfDataContext dataContext)
        {
            var hasStartLog = _approvalLogRepo.Find(x => x.DataId == dataContext.DataId && x.NodeType == WfNodeType.Start).Any();
            if (hasStartLog)
            {
                return;
            }

            var definition = GetWorkflowDefinition(workflowInstance);
            var startStep = definition?.Metadata?.Steps?.FirstOrDefault(x => x.NodeType == WfNodeType.Start);
            if (startStep == null || dataContext.WfStarter == null)
            {
                return;
            }

            using var scope = _approvalLogRepo.NewTransactionScope();
            _approvalLogRepo.Insert(new Wf_ApprovalLog
            {
                CorpId = dataContext.CorpId,
                AppId = dataContext.AppId,
                FormId = dataContext.FormId,
                FormName = GetFormDef(dataContext.FormId)?.Name ?? string.Empty,
                DataId = dataContext.DataId,
                DataBrief = GetDataBrief(dataContext.FormId, dataContext.DataId),
                Approver = dataContext.WfStarter,
                NodeId = startStep.Id,
                NodeName = startStep.Name,
                NodeType = WfNodeType.Start,
                Comment = string.Empty,
                Signature = string.Empty,
                ApprovalTime = DateTime.UtcNow.ToTimeStampMs(),
                Result = ApproveAction.Approve,
                WfVersion = workflowInstance.Version,
                Round = 1,
            }, scope.SessionHandle);
            scope.CommitTransaction();
        }

        private async Task<string> ResolveExpireReturnTargetNodeIdAsync(WorkflowInstance workflowInstance, Wf_Todo todo, ReturnSetting? returnSetting)
        {
            var dataContext = WfDataContext.FromExpando((ExpandoObject)workflowInstance.Data);
            await EnsureStartApprovalLogAsync(workflowInstance, dataContext);

            var trail = GetReturnTrail(workflowInstance, todo, dataContext.Round)
                .Where(x => x.NodeId != todo.ApproveNodeId)
                .ToList();
            if (trail.Count == 0)
            {
                throw new InvalidOperationException("审批超时自动回退失败：没有可回退节点");
            }

            return (returnSetting?.TargetMode ?? ReturnTargetMode.Previous) switch
            {
                ReturnTargetMode.Start => trail.FirstOrDefault(x => x.NodeType == WfNodeType.Start)?.NodeId
                    ?? throw new InvalidOperationException("审批超时自动回退失败：未找到发起节点"),
                ReturnTargetMode.Specified => trail.FirstOrDefault(x => x.NodeId == returnSetting?.TargetNodeId)?.NodeId
                    ?? throw new InvalidOperationException("审批超时自动回退失败：指定回退节点不可达"),
                _ => trail.Last().NodeId
            };
        }

        private List<Wf_ApprovalLog> GetReturnTrail(WorkflowInstance workflowInstance, Wf_Todo todo, int currentRound)
        {
            var definition = GetWorkflowDefinition(workflowInstance) ?? throw new InvalidOperationException("流程定义不存在");
            var startNodeId = definition.Metadata?.Steps?.FirstOrDefault(x => x.NodeType == WfNodeType.Start)?.Id;
            if (string.IsNullOrWhiteSpace(startNodeId))
            {
                throw new InvalidOperationException("流程定义缺少发起节点");
            }

            var round = currentRound;
            List<Wf_ApprovalLog> logs;
            do
            {
                logs = _approvalLogRepo.Find(x => x.DataId == todo.DataId && x.Round == round)
                    .SortBy(x => x.ApprovalTime)
                    .ToList();
                if (logs.Any(x => x.NodeId == startNodeId))
                {
                    break;
                }
                round -= 1;
            } while (round > 0);

            var path = BuildFlowPath(definition, startNodeId, todo.ApproveNodeId);
            return logs
                .Where(x => (x.NodeType == WfNodeType.Start || x.NodeType == WfNodeType.Approve) && path.Contains(x.NodeId))
                .ToList();
        }

        public static HashSet<string> BuildFlowPath(Wf_Definition definition, string startNodeId, string currentNodeId)
        {
            var steps = definition.Metadata?.Steps?.ToDictionary(x => x.Id) ?? new Dictionary<string, WfStep>();
            var reverse = new Dictionary<string, HashSet<string>>();
            foreach (var step in steps.Values)
            {
                if (!string.IsNullOrWhiteSpace(step.NextStepId))
                {
                    AddReverseEdge(reverse, step.NextStepId, step.Id);
                }
                foreach (var next in step.SelectNextStep.Keys)
                {
                    AddReverseEdge(reverse, next, step.Id);
                }
            }

            var result = new HashSet<string>();
            var queue = new Queue<string>();
            queue.Enqueue(currentNodeId);
            while (queue.Count > 0)
            {
                var nodeId = queue.Dequeue();
                if (!result.Add(nodeId))
                {
                    continue;
                }
                if (nodeId == startNodeId)
                {
                    continue;
                }
                if (!reverse.TryGetValue(nodeId, out var prevs))
                {
                    continue;
                }
                foreach (var prev in prevs)
                {
                    queue.Enqueue(prev);
                }
            }
            return result;
        }

        public static void AddReverseEdge(Dictionary<string, HashSet<string>> reverse, string nextStepId, string stepId)
        {
            if (!reverse.TryGetValue(nextStepId, out var prevs))
            {
                prevs = [];
                reverse[nextStepId] = prevs;
            }
            prevs.Add(stepId);
        }

        private async Task<Wf_Todo?> CreateTodoForNodeAsync(WorkflowInstance workflowInstance, WfDataContext dataContext, string nodeId, IClientSessionHandle session)
        {
            var definition = GetWorkflowDefinition(workflowInstance);
            var step = definition?.Metadata?.Steps?.FirstOrDefault(x => x.Id == nodeId);
            if (step == null || step.NodeType != WfNodeType.Approve)
            {
                return null;
            }

            var empIds = await PopulateEmpIds(dataContext, step.WfNodeSetting?.ApproveSetting?.Candidates);
            var employeeId = empIds.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(employeeId))
            {
                return null;
            }

            var now = DateTime.UtcNow.ToTimeStampMs();
            var todo = new Wf_Todo
            {
                CorpId = dataContext.CorpId,
                AppId = dataContext.AppId,
                FormId = dataContext.FormId,
                DataId = dataContext.DataId,
                WfInstanceId = workflowInstance.Id,
                ApproveNodeId = step.Id,
                ApproveNodeName = step.Name,
                EmployeeId = employeeId,
                CreateTime = now,
                UpdateTime = now,
                Starter = dataContext.WfStarter,
                ApproveNodeStartTime = now,
                DataBrief = GetDataBrief(dataContext.FormId, dataContext.DataId),
                ExpireHandled = false,
            };
            _todoRepo.Insert(todo, session);
            return todo;
        }

        private static Wf_Todo CloneTodo(Wf_Todo source, string employeeId)
        {
            var now = DateTime.UtcNow.ToTimeStampMs();
            return new Wf_Todo
            {
                CorpId = source.CorpId,
                AppId = source.AppId,
                FormId = source.FormId,
                DataId = source.DataId,
                WfInstanceId = source.WfInstanceId,
                ApproveNodeId = source.ApproveNodeId,
                ApproveNodeName = source.ApproveNodeName,
                EmployeeId = employeeId,
                CreateTime = now,
                UpdateTime = now,
                Starter = source.Starter,
                ApproveNodeStartTime = now,
                DataBrief = source.DataBrief,
                ExpireTime = source.ExpireTime,
                ExpireHandled = false,
            };
        }

        private Wf_ApprovalLog CreateApprovalLog(WorkflowActionDataContext context, WorkflowInstance workflowInstance, Wf_Todo todo, WfNodeType nodeType, string nodeId, string nodeName, ApproveAction result, string comment, int round)
        {
            return new Wf_ApprovalLog
            {
                CorpId = context.CorpId,
                AppId = todo.AppId,
                FormId = todo.FormId,
                FormName = GetFormDef(todo.FormId)?.Name ?? string.Empty,
                DataId = todo.DataId,
                WfVersion = workflowInstance.Version,
                NodeId = nodeId,
                NodeName = nodeName,
                NodeType = nodeType,
                Round = round,
                Approver = context.CurrentEmployee,
                Result = result,
                Comment = comment,
                ApprovalTime = DateTime.UtcNow.ToTimeStampMs(),
                DataBrief = todo.DataBrief,
            };
        }

        private FormDef? GetFormDef(string formId) => _formDefRepo.Get(formId);

        private List<BriefField> GetDataBrief(string formId, string dataId)
        {
            var brief = new List<BriefField>();
            var form = _formDefRepo.Get(formId);
            var data = _formDataRepo.Get(dataId);
            if (form?.Content.Items?.Count > 0 && data != null)
            {
                foreach (var field in form.Content.Items.Take(6))
                {
                    brief.Add(new BriefField { Field = field.Field, Title = field.Title, Value = data.Data.GetValueOrDefault(field.Field) });
                }
            }
            return brief;
        }

        private void UpdateFormStatus(string dataId, FlowStatus flowStatus, IClientSessionHandle session)
        {
            _formDataRepo.Update(dataId, Builders<FormData>.Update.Set(x => x.FlowStatus, flowStatus), session: session);
        }

        private Wf_Definition? GetWorkflowDefinition(WorkflowInstance wfInst)
        {
            return _definitionService.Find(x => x.ExternalId == wfInst.WorkflowDefinitionId && x.Version == wfInst.Version).FirstOrDefault();
        }

        private static void ResetWorkflowPointers(WorkflowInstance wfInst, Wf_Definition? definition, string? targetNodeId = null)
        {
            var target = FindWorkflowCoreStep(definition?.Metadata?.Steps, targetNodeId);
            if (target == null)
            {
                throw new InvalidOperationException("流程定义缺少目标节点，无法重置流程实例");
            }
            var (targetStep, stepId) = target.Value;

            wfInst.ExecutionPointers.Clear();
            wfInst.ExecutionPointers.Add(new ExecutionPointer
            {
                Id = ObjectId.GenerateNewId().ToString(),
                StepId = stepId,
                StepName = targetStep.Name,
                Active = true,
                Status = PointerStatus.Pending,
                Children = [],
                Scope = []
            });
        }

        private static (WfStep Step, int StepId)? FindWorkflowCoreStep(IEnumerable<WfStep>? source, string? targetNodeId)
        {
            if (source == null)
            {
                return null;
            }

            var stack = new Stack<WfStep>(source.Reverse());
            var stepId = 0;
            while (stack.Count > 0)
            {
                var nextStep = stack.Pop();
                var isMatch = string.IsNullOrWhiteSpace(targetNodeId)
                    ? nextStep.NodeType == WfNodeType.Start
                    : string.Equals(nextStep.Id, targetNodeId, StringComparison.OrdinalIgnoreCase);
                if (isMatch)
                {
                    return (nextStep, stepId);
                }

                if (nextStep.Work != null)
                {
                    foreach (var branch in nextStep.Work)
                    {
                        foreach (var child in branch.Reverse<WfStep>())
                        {
                            stack.Push(child);
                        }
                    }
                }

                if (nextStep.CompensateWith != null)
                {
                    foreach (var child in nextStep.CompensateWith.Reverse<WfStep>())
                    {
                        stack.Push(child);
                    }
                }

                stepId++;
            }

            return null;
        }
    }
}
