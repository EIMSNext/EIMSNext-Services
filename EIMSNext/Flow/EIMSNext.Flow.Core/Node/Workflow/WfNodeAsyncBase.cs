using System.Dynamic;
using System.Text.Json;

using HKH.Common;
using HKH.Mef2.Integration;

using EIMSNext.Core;
using EIMSNext.Core.Entity;
using EIMSNext.Core.Query;
using EIMSNext.Core.Repository;
using EIMSNext.Entity;
using EIMSNext.Flow.Core.Interface;

using Microsoft.Extensions.Logging;

using MongoDB.Driver;

using WorkflowCore.Interface;
using WorkflowCore.Models;
using EIMSNext.Common.Extension;
using EIMSNext.Service.Interface;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace EIMSNext.Flow.Core.Node
{
    public abstract class WfNodeAsyncBase<T> : NodeAsyncBase where T : NodeAsyncBase
    {
        protected WfNodeAsyncBase(IResolver resolver) : base(resolver)
        {
            TodoRepository = resolver.GetRepository<Wf_Todo>();
            ExecLogRepository = resolver.GetRepository<Wf_ExecLog>();
            ApprovalLogRepository = resolver.GetRepository<Wf_ApprovalLog>();
            FormDataRepository = resolver.GetRepository<FormData>();
            FormDefRepository = resolver.GetRepository<FormDef>();
            EmployeeRepository = resolver.GetRepository<Employee>();
            Logger = resolver.GetLogger<T>();
        }

        protected IRepository<Wf_Todo> TodoRepository { get; private set; }
        protected IRepository<Wf_ExecLog> ExecLogRepository { get; private set; }
        protected IRepository<Wf_ApprovalLog> ApprovalLogRepository { get; private set; }
        protected IRepository<FormData> FormDataRepository { get; private set; }
        protected IRepository<FormDef> FormDefRepository { get; private set; }
        protected IRepository<Employee> EmployeeRepository { get; private set; }
        protected IDataflowRunner DataflowRunner => Resolver.Resolve<IDataflowRunner>();

        protected ILogger<T> Logger { get; private set; }
        private FormData? FormData { get; set; }
        private FormDef? FormDef { get; set; }

        protected WfDataContext GetDataContext(IStepExecutionContext context)
        {
            return WfDataContext.FromExpando((ExpandoObject)context.Workflow.Data);
        }

        protected void AddApprovalLog(WorkflowInstance wfInst, Wf_Todo todoTask, WfDataContext dataContext, WfStep wfStep, WfApproveData approveData, IClientSessionHandle? session)
        {
            var log = new Wf_ApprovalLog()
            {
                CorpId = dataContext.CorpId,
                AppId = dataContext.AppId,
                FormId = dataContext.FormId,
                FormName = GetFormDef(dataContext.FormId).Name,
                DataId = dataContext.DataId,
                DataBrief = todoTask.DataBrief,
                Approver = new Operator(dataContext.CorpId, approveData.UserId, approveData.WorkerId, approveData.WorkerName),
                NodeId = wfStep.Id,
                NodeName = wfStep.Name,
                NodeType = wfStep.NodeType,
                Comment = approveData.Comment,
                Signature = approveData.Signature,
                ApprovalTime = DateTime.UtcNow.ToTimeStampMs(),
                Result = approveData.Action,
                WfVersion = wfInst.Version,
                Round = 1
            };

            ApprovalLogRepository.Insert(log, session);
        }

        protected ExecutionResult RewaitActivity(IStepExecutionContext context)
        {
            context.ExecutionPointer.EventPublished = false;
            context.ExecutionPointer.EventData = null;

            return ExecutionResult.WaitForActivity(context.ExecutionPointer.EventKey, context.Workflow.Data, DateTime.Now);
        }

        protected async Task CreateTodos(WorkflowInstance wfInst, WfDataContext dataContext, WfStep wfStep, IClientSessionHandle? session)
        {
            var empIds = await PopulateEmpIds(dataContext, wfStep.WfNodeSetting?.ApproveSetting?.Candidates);

            var todos = new List<Wf_Todo>();
            empIds.ForEach(empId =>
            {
                todos.Add(new Wf_Todo
                {
                    CorpId = dataContext.CorpId,
                    AppId = dataContext.AppId,
                    FormId = dataContext.FormId,
                    DataId = dataContext.DataId,
                    WfInstanceId = wfInst.Id,
                    ApproveNodeId = wfStep.Id,
                    ApproveNodeName = wfStep.Name,
                    EmployeeId = empId,
                    CreateTime = DateTime.UtcNow.ToTimeStampMs(),
                    UpdateTime = DateTime.UtcNow.ToTimeStampMs(),
                    Starter = dataContext.WfStarter,
                    ApproveNodeStartTime = DateTime.UtcNow.ToTimeStampMs(),
                    DataBrief = GetDataBrief(dataContext.FormId, dataContext.DataId)
                });
            });

            if ((todos.Any()))
            {
                TodoRepository.Insert(todos, session);
            }
        }

        protected async Task<IEnumerable<string>> PopulateEmpIds(WfDataContext dataContext, IList<ApprovalCandidate>? candidates)
        {
            var empIds = new List<string>();
            if (candidates?.Count > 0)
            {
                var deptIds = new List<string>();
                var roleIds = new List<string>();

                var formData = GetFormData(dataContext.DataId);

                candidates.ForEach(c =>
                {
                    switch (c.CandidateType)
                    {
                        case CandidateType.Department:
                            deptIds.Add(c.CandidateId);
                            break;
                        case CandidateType.Role:
                            roleIds.Add(c.CandidateId);
                            break;
                        case CandidateType.Employee:
                            empIds.Add(c.CandidateId);
                            break;
                        case CandidateType.Dynamic:
                            //TODO: 进一步计算
                            if (c.CandidateId == "starter" && dataContext.WfStarter != null)
                            {
                                //TODO:此处应进一步的排除匿名，比如由数据流节点或其他系统任务发起的流程
                                empIds.Add(dataContext.WfStarter.EmpId);
                            }
                            break;
                    }
                });

                if (deptIds.Any())
                {
                    await EmployeeRepository.Find(x => deptIds.Contains(x.DepartmentId)).ForEachAsync(x => empIds.Add(x.Id));
                }

                if (roleIds.Any())
                {
                    await EmployeeRepository.Find(new MongoFindOptions<Employee>
                    {
                        Filter = Builders<Employee>.Filter.ElemMatch(x => x.Roles, r => roleIds.Contains(r.RoleId))
                    }).ForEachAsync(x => empIds.Add(x.Id));
                }
            }

            //只取前200人
            return empIds.Take(200);
        }

        public DeleteResult DeleteTodos(string corpId, string dataId, string nodeId, IClientSessionHandle? session)
        {
            var filter = new DynamicFilter()
            {
                Items = new List<DynamicFilter> {
                new DynamicFilter() { Field = "CorpId", Op = FilterOp.Eq, Value = corpId },
                new DynamicFilter() { Field = "DataId", Op = FilterOp.Eq, Value = dataId },
                new DynamicFilter() { Field = "ApproveNodeId", Op = FilterOp.Eq, Value = nodeId }
            }
            };

            return TodoRepository.Delete(filter, session);
        }

        public UpdateResult UpdateWorkflowStatus(string corpId, string dataId, FlowStatus flowStatus, IClientSessionHandle? session)
        {
            return FormDataRepository.Update(dataId, Builders<FormData>.Update.Set(x => x.FlowStatus, flowStatus), session: session);
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
                Logger.LogError(ex, $"写入审批流程执行日志失败：{JsonSerializer.Serialize(execLog)}");
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

        protected async Task RunDataflow(FormData formData, EventSourceType eventSource, EventType eventType, string wfNodeId, Operator? starter, CascadeMode cascade, string? eventIds)
        {
            var dfExecResult = await DataflowRunner.RunAsync(formData, eventSource, eventType, wfNodeId, starter, cascade, eventIds);
            if (!dfExecResult.Success)
            {
                throw new UnLogException(dfExecResult.Error);
            }
        }
    }
}
