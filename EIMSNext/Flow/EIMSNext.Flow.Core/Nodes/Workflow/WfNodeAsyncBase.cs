using EIMSNext.Common.Extensions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Flow.Core.Interfaces;
using EIMSNext.Service.Entities;
using HKH.Common;
using HKH.Mef2.Integration;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
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
        protected IDataflowRunner DataflowRunner => Resolver.Resolve<IDataflowRunner>();

        protected ILogger<T> Logger { get; private set; }
        private FormData? FormData { get; set; }
        private FormDef? FormDef { get; set; }

        protected WfDataContext GetDataContext(IStepExecutionContext context)
        {
            return WfDataContext.FromExpando((ExpandoObject)context.Workflow.Data);
        }

        protected void AddTaskLog(WorkflowInstance wfInst, Wf_Task task, WfDataContext dataContext, WfStep wfStep, WfApproveData approveData, IClientSessionHandle? session)
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

            TaskLogRepository.Insert(log, session);
        }

        protected async Task AddCCLogs(WorkflowInstance wfInst, WfDataContext dataContext, WfStep wfStep, IEnumerable<string> empIds, IClientSessionHandle? session)
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
            await EmployeeRepository.Find(x => targetEmpIds.Contains(x.Id))
             .ForEachAsync(emp => logs.Add(new Wf_TaskLog()
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
              }));

            logs = logs.Where(x => x.Approver != null && !existedEmpIds.Contains(x.Approver.Id)).ToList();

            if (logs.Any())
            {
                TaskLogRepository.Insert(logs, session);
            }
        }

        protected ExecutionResult RewaitActivity(IStepExecutionContext context)
        {
            context.ExecutionPointer.EventPublished = false;
            context.ExecutionPointer.EventData = null;

            return ExecutionResult.WaitForActivity(context.ExecutionPointer.EventKey, context.Workflow.Data, DateTime.Now);
        }

        protected async Task<List<Wf_Task>> CreateTasks(WorkflowInstance wfInst, WfDataContext dataContext, WfStep wfStep, IClientSessionHandle? session)
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
                TaskRepository.Insert(tasks, session);
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

        public DeleteResult DeleteTasks(string corpId, string dataId, string nodeId, IClientSessionHandle? session)
        {
            var filter = new DynamicFilter()
            {
                Items = new List<DynamicFilter> {
                new DynamicFilter() { Field = "CorpId", Op = FilterOp.Eq, Value = corpId },
                new DynamicFilter() { Field = "DataId", Op = FilterOp.Eq, Value = dataId },
                new DynamicFilter() { Field = "ApproveNodeId", Op = FilterOp.Eq, Value = nodeId }
            }
            };

            return TaskRepository.Delete(filter, session);
        }

        public UpdateResult UpdateWorkflowStatus(string corpId, string dataId, FlowStatus flowStatus, IClientSessionHandle? session)
        {
            return FormDataRepository.Update(dataId, Builders<FormData>.Update.Set(x => x.FlowStatus, flowStatus), session: session);
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
                var lastApproval = TaskLogRepository
                    .Find(x => x.DataId == dataContext.DataId
                        && x.Result != ApproveAction.CopyTo
                        && x.Result != ApproveAction.Transfer
                        && x.Result != ApproveAction.AutoTransfer
                        && x.Result != ApproveAction.ChangeApprover)
                    .SortByDescending(x => x.ApprovalTime)
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

        protected async Task RunDataflow(DfRunParamter paramter)
        {
            var dfExecResult = await DataflowRunner.RunAsync(paramter);
            if (!dfExecResult.Success)
            {
                throw new UnLogException(dfExecResult.Error);
            }
        }
    }
}
