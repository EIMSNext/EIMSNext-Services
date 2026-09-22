using EIMSNext.Common.Extensions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Flow.Core.Interfaces;
using EIMSNext.Entities;
using HKH.Common;
using HKH.Mef2.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Dynamic;
using WorkflowCore.Interface;
using WorkflowCore.Models;
using WorkflowCore.Primitives;

namespace EIMSNext.Flow.Core.Nodes
{
    public abstract class WfNodeAsyncBase<T> : NodeAsyncBase where T : NodeAsyncBase
    {
        protected WfNodeAsyncBase(IResolver resolver) : base(resolver)
        {
            TaskRepository = resolver.GetRepository<Wf_Task>();
            ExecLogRepository = resolver.GetRepository<Wf_ExecLog>();
            TaskLogRepository = resolver.GetRepository<Wf_TaskLog>();
            FormDataRepository = resolver.GetRepository<FormData>();
            FormDefRepository = resolver.GetRepository<FormDef>();
            EmployeeRepository = resolver.GetRepository<Employee>();
            EmployeeDepartmentRepository = resolver.GetRepository<EmployeeDepartment>();
            DepartmentRepository = resolver.GetRepository<Department>();
            Logger = resolver.GetLogger<T>();
        }

        protected IRepository<Wf_Task> TaskRepository { get; private set; }
        protected IRepository<Wf_ExecLog> ExecLogRepository { get; private set; }
        protected IRepository<Wf_TaskLog> TaskLogRepository { get; private set; }
        protected IRepository<FormData> FormDataRepository { get; private set; }
        protected IRepository<FormDef> FormDefRepository { get; private set; }
        protected IRepository<Employee> EmployeeRepository { get; private set; }
        protected IRepository<EmployeeDepartment> EmployeeDepartmentRepository { get; private set; }
        protected IRepository<Department> DepartmentRepository { get; private set; }
        protected IEventFlowRunner EventFlowRunner => Resolver.Resolve<IEventFlowRunner>();

        protected ILogger<T> Logger { get; private set; }
        private FormData? FormData { get; set; }
        private FormDef? FormDef { get; set; }

        protected WfDataContext GetDataContext(IStepExecutionContext context)
        {
            return WfDataContext.FromData((IDictionary<string, object?>)context.Workflow.Data);
        }

        /// <summary>
        /// 写入审批日志。
        /// </summary>
        /// <param name="wfInst">工作流实例。</param>
        /// <param name="task">审批任务。</param>
        /// <param name="dataContext">数据上下文。</param>
        /// <param name="wfStep">节点定义。</param>
        /// <param name="approveData">审批数据。</param>
        /// <remarks>
        /// 原签名末尾有一个 <c>IClientSessionHandle? session</c> 参数：Mongo 需要它才能把写入
        /// 挂到调用方的事务上。PostgreSQL 下事务绑在 <see cref="DbContext"/> 的当前连接上，
        /// 仓储写方法会自行判断 <see cref="TransactionScope.IsInTransaction"/> 决定是
        /// 加入外层事务还是开一个短事务，因此 session 参数被整体移除。
        /// </remarks>
        protected async Task AddTaskLog(WorkflowInstance wfInst, Wf_Task task, WfDataContext dataContext, WfStep wfStep, WfApproveData approveData)
        {
            var log = new Wf_TaskLog()
            {
                CorpId = dataContext.CorpId,
                AppId = dataContext.AppId,
                FormId = dataContext.FormId,
                FormName = GetFormDef(dataContext.FormId).Name,
                DataId = dataContext.DataId,
                DataBrief = task.DataBrief,
                Approver = new Operator(approveData.WorkerId, approveData.WorkerCode, approveData.WorkerName),
                NodeId = wfStep.Id,
                NodeName = wfStep.Name,
                NodeType = wfStep.NodeType,
                Comment = approveData.Comment,
                Signature = approveData.Signature,
                ApprovalTime = DateTime.UtcNow.ToTimeStampMs(),
                Result = approveData.Action,
                WfVersion = wfInst.Version,
                Round = dataContext.Round
            };

            await TaskLogRepository.InsertAsync(log);
        }

        protected async Task AddCCLogs(WorkflowInstance wfInst, WfDataContext dataContext, WfStep wfStep, IEnumerable<string> empIds)
        {
            var targetEmpIds = empIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            if (targetEmpIds.Count == 0)
            {
                return;
            }

            var existedEmpIds = TaskLogRepository.Find(x =>
                x.DataId == dataContext.DataId
                && x.NodeId == wfStep.Id
                && x.Result == ApproveAction.CopyTo
                && x.Round == dataContext.Round
                && x.Approver != null
                && targetEmpIds.Contains(x.Approver.Id))
                .ToList()
                .Select(x => x.Approver?.Id ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var logs = new List<Wf_TaskLog>();
            var employees = await EmployeeRepository.Find(x => targetEmpIds.Contains(x.Id)).ToListAsync();
            foreach (var emp in employees)
            {
                logs.Add(new Wf_TaskLog()
                {
                    CorpId = dataContext.CorpId,
                    AppId = dataContext.AppId,
                    FormId = dataContext.FormId,
                    FormName = GetFormDef(dataContext.FormId).Name,
                    DataId = dataContext.DataId,
                    DataBrief = GetDataBrief(dataContext.FormId, dataContext.DataId),
                    Approver = new Operator(emp.Id, emp.Code, emp.EmpName),
                    NodeId = wfStep.Id,
                    NodeName = wfStep.Name,
                    NodeType = wfStep.NodeType,
                    ApprovalTime = DateTime.UtcNow.ToTimeStampMs(),
                    Result = ApproveAction.CopyTo,
                    WfVersion = wfInst.Version,
                    Round = dataContext.Round
                });
            }

            logs = logs.Where(x => x.Approver != null && !existedEmpIds.Contains(x.Approver.Id)).ToList();

            if (logs.Any())
            {
                await TaskLogRepository.InsertAsync(logs);
            }
        }

        protected ExecutionResult RewaitActivity(IStepExecutionContext context)
        {
            context.ExecutionPointer.EventPublished = false;
            context.ExecutionPointer.EventData = null;

            return ExecutionResult.WaitForActivity(context.ExecutionPointer.EventKey, context.Workflow.Data, DateTime.Now);
        }

        protected async Task<List<Wf_Task>> CreateTasks(WorkflowInstance wfInst, WfDataContext dataContext, WfStep wfStep)
        {
            var approveSetting = wfStep.WfNodeSetting?.ApproveSetting;
            var empIds = (await PopulateEmpIds(dataContext, approveSetting?.Candidates)).ToList();
            if (!empIds.Any() && approveSetting?.NoApproverSetting?.ActionType == NoApproverActionType.TransferToMember)
            {
                empIds = (await PopulateEmpIds(dataContext, approveSetting.NoApproverSetting.Candidates)).ToList();
            }

            var tasks = new List<Wf_Task>();
            var now = DateTime.UtcNow.ToTimeStampMs();
            var expireTime = GetExpireTime(approveSetting);
            empIds.ForEach(empId =>
            {
                tasks.Add(new Wf_Task
                {
                    CorpId = dataContext.CorpId,
                    AppId = dataContext.AppId,
                    FormId = dataContext.FormId,
                    DataId = dataContext.DataId,
                    WfInstanceId = wfInst.Id,
                    ApproveNodeId = wfStep.Id,
                    ApproveNodeName = wfStep.Name,
                    EmployeeId = empId,
                    CreateTime = now,
                    UpdateTime = now,
                    Starter = dataContext.WfStarter,
                    ApproveNodeStartTime = now,
                    DataBrief = GetDataBrief(dataContext.FormId, dataContext.DataId),
                    ExpireTime = expireTime,
                    ExpireHandled = false
                });
            });

            if ((tasks.Any()))
            {
                await TaskRepository.InsertAsync(tasks);
            }

            return tasks;
        }

        private static long? GetExpireTime(ApproveSetting? approveSetting)
        {
            var expireSetting = approveSetting?.ExpireSetting;
            if (expireSetting == null || expireSetting.TimeValue <= 0)
            {
                return null;
            }

            var utcNow = DateTime.UtcNow;
            var expireAt = expireSetting.TimeUnit switch
            {
                TimeUnit.Minute => utcNow.AddMinutes(expireSetting.TimeValue),
                TimeUnit.Hour => utcNow.AddHours(expireSetting.TimeValue),
                TimeUnit.Day => utcNow.AddDays(expireSetting.TimeValue),
                _ => utcNow
            };

            return expireAt.ToTimeStampMs();
        }

        protected async Task<IEnumerable<string>> PopulateEmpIds(WfDataContext dataContext, IList<ApprovalCandidate>? candidates)
        {
            var resolver = new WorkflowCandidateResolver(EmployeeRepository, EmployeeDepartmentRepository, DepartmentRepository, FormDefRepository, FormDataRepository);
            return await resolver.ResolveEmployeeIdsAsync(dataContext, candidates);
        }

        /// <summary>
        /// 删除指定节点下某人/某数据的所有待办任务。
        /// </summary>
        /// <param name="corpId">企业 ID。</param>
        /// <param name="dataId">数据 ID。</param>
        /// <param name="nodeId">节点 ID。</param>
        /// <returns>受影响行数。</returns>
        public async Task<int> DeleteTasks(string corpId, string dataId, string nodeId)
        {
            return await TaskRepository.DeleteManyAsync(x =>
                x.CorpId == corpId && x.DataId == dataId && x.ApproveNodeId == nodeId);
        }

        /// <summary>
        /// 抢占并删除一条待办任务（原子「领取」语义）。
        /// </summary>
        /// <param name="workflowInstanceId">工作流实例 ID。</param>
        /// <param name="dataId">数据 ID。</param>
        /// <param name="nodeId">节点 ID。</param>
        /// <param name="employeeId">员工 ID。</param>
        /// <returns>被抢到的任务；不存在时为 null。</returns>
        /// <remarks>
        /// <para>
        /// 原实现是 Mongo 的 <c>FindOneAndDelete</c>——「查到就删、删掉的那条返回给你」，
        /// 天然原子，是多人并发审批同一节点时防重复提交的关键。
        /// </para>
        /// <para>
        /// PostgreSQL 下 EF Core 没有等价的 <c>DELETE ... RETURNING *</c> 高层 API
        /// （<c>ExecuteDelete</c> 只返回行数），因此改为「先取行 → 按主键条件删除 → 只在
        /// 删除成功时返回该行」：
        /// </para>
        /// <code language="csharp">
        /// var task = await TaskRepository.Find(predicate).FirstOrDefaultAsync();
        /// if (task is null) return null;
        /// var affected = await TaskRepository.DeleteManyAsync(x =&gt; x.Id == task.Id);
        /// return affected == 1 ? task : null;
        /// </code>
        /// <para>
        /// <b>并发安全性说明：</b>两个并发请求都可能读到同一行，但 <c>delete where "Id" = @id</c>
        /// 只有一个能返回 1，另一个返回 0 从而返回 null，最终语义与 <c>FindOneAndDelete</c> 一致。
        /// 前提是调用方处于事务内（由 <c>SubmitAsync</c> 打开），否则两次读之间可能被第三方改写。
        /// 若后续要彻底消除窗口，可改为一条 <c>DELETE ... WHERE "Id" = (SELECT ... FOR UPDATE SKIP LOCKED)
        /// RETURNING *</c> 的原生 SQL，当前实现已满足业务要求。
        /// </para>
        /// </remarks>
        protected async Task<Wf_Task?> ClaimTask(string workflowInstanceId, string dataId, string nodeId, string employeeId)
        {
            var task = await TaskRepository
                .Find(x => x.WfInstanceId == workflowInstanceId
                           && x.DataId == dataId
                           && x.ApproveNodeId == nodeId
                           && x.EmployeeId == employeeId)
                .FirstOrDefaultAsync();

            if (task is null)
            {
                return null;
            }

            var taskId = task.Id;
            var affected = await TaskRepository.DeleteManyAsync(x => x.Id == taskId);
            return affected == 1 ? task : null;
        }

        /// <summary>
        /// 更新表单数据的流程状态。
        /// </summary>
        /// <param name="corpId">企业 ID。</param>
        /// <param name="dataId">数据 ID。</param>
        /// <param name="flowStatus">流程状态。</param>
        /// <returns>受影响行数。</returns>
        public async Task<int> UpdateWorkflowStatus(string corpId, string dataId, FlowStatus flowStatus)
        {
            return await FormDataRepository.UpdateAsync(
                dataId,
                setters => setters.SetProperty(x => x.FlowStatus, flowStatus));
        }

        protected Wf_Definition? GetWorkflowDefinition(WorkflowInstance wfInst)
        {
            var defRepo = Resolver.GetRepository<Wf_Definition>();
            return defRepo.Find(x => x.ExternalId == wfInst.WorkflowDefinitionId && x.Version == wfInst.Version).FirstOrDefault();
        }

        protected bool ShouldAutoApprove(WorkflowInstance wfInst, WfDataContext dataContext, WfStep wfStep)
        {
            var definition = GetWorkflowDefinition(wfInst);
            var rule = definition?.Metadata?.WorkflowSetting?.AutoProcessRule ?? WorkflowAutoProcessRule.Disabled;
            if (rule == WorkflowAutoProcessRule.Disabled)
            {
                return false;
            }

            if (rule == WorkflowAutoProcessRule.FirstNodeOnly)
            {
                var firstApproveNodeId = definition?.Metadata?.Steps?.FirstOrDefault(x => x.NodeType == WfNodeType.Approve)?.Id;
                return !string.IsNullOrWhiteSpace(firstApproveNodeId) && firstApproveNodeId == wfStep.Id;
            }

            if (rule == WorkflowAutoProcessRule.ContinuousApproval)
            {
                // Mongo 时期的 SortByDescending(...).FirstOrDefault() 换成
                // OrderByDescending(...).FirstOrDefault()，两者都翻译为 ORDER BY ... LIMIT 1。
                var lastApproval = TaskLogRepository
                    .Find(x => x.DataId == dataContext.DataId
                        && x.Result != ApproveAction.CopyTo
                        && x.Result != ApproveAction.Transfer
                        && x.Result != ApproveAction.AutoTransfer
                        && x.Result != ApproveAction.ChangeApprover)
                    .OrderByDescending(x => x.ApprovalTime)
                    .FirstOrDefault();
                return lastApproval?.Approver?.Id == dataContext.WfStarter?.Id;
            }

            return false;
        }

        protected void CreateExecLog(WorkflowInstance wfInst, WfDataContext dataContext, WfStep wfStep, WfApproveData approveData, string errMsg = "")
        {
            Wf_ExecLog? execLog = null;
            try
            {
                execLog = new Wf_ExecLog() { Id = approveData.ExecLogId, DataId = dataContext.DataId, WfInstanceId = wfInst.Id, EmpId = approveData.WorkerId, NodeId = wfStep.Id, ExecTime = DateTime.UtcNow.ToTimeStampMs(), ErrMsg = errMsg, Success = string.IsNullOrEmpty(errMsg) };
                ExecLogRepository.Insert(execLog);
            }
            catch (Exception ex)    //写日志失败不影响整个审批流程
            {
                Logger.LogError(ex, "写入审批流程执行日志失败。ExecLog={ExecLog}", execLog);
            }
        }

        protected FormData GetFormData(string dataId)
        {
            if (FormData == null)
                FormData = FormDataRepository.Get(dataId);
            return FormData!;
        }
        protected FormDef GetFormDef(string formId)
        {
            if (FormDef == null)
                FormDef = FormDefRepository.Get(formId);
            return FormDef!;
        }
        protected List<BriefField> GetDataBrief(string formId, string dataId)
        {
            var brief = new List<BriefField>();

            var form = GetFormDef(formId);
            var data = GetFormData(dataId);

            if (form.Content.Items?.Count > 0)
            {
                var max = 6;
                var i = 0;
                foreach (var field in form.Content.Items)
                {
                    i++;
                    if (i > max) break;

                    brief.Add(new BriefField { Field = field.Field, Title = field.Title, Value = data.Data.GetValueOrDefault(field.Field) });
                }
            }

            return brief;
        }

        protected async Task RunEventFlow(EfRunParameter paramter)
        {
            var isWorkflowTransition = paramter.WorkflowTransition
                && !string.IsNullOrWhiteSpace(paramter.WfNodeId)
                && !string.IsNullOrWhiteSpace(paramter.NodeAction);
            EfExecResult efExecResult;
            if (isWorkflowTransition)
            {
                efExecResult = await EventFlowRunner.RunAsync(paramter);
            }
            else
            {
                using var suppression = TransactionScope.SuppressAmbient();
                efExecResult = await EventFlowRunner.RunAsync(paramter);
            }
            if (!efExecResult.Success)
            {
                throw new UnLogException(efExecResult.Error);
            }
        }
    }
}
