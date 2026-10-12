using Asp.Versioning;

using EIMSNext.ApiHost.Controllers;
using EIMSNext.ApiHost.Extensions;
using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Flow.Core;
using EIMSNext.Flow.Core.Interfaces;
using EIMSNext.Flow.Persistence;
using EIMSNext.Service.Contracts;
using EIMSNext.Entities;

using HKH.Mef2.Integration;

using Microsoft.AspNetCore.Mvc;

using Microsoft.EntityFrameworkCore;

using System.Dynamic;

using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Host.Controllers
{
    [ApiVersion(1.0)]
    public class WorkflowController : MefControllerBase
    {
        private readonly IWorkflowHost _wfHost;
        private readonly IWorkflowLoader _workflowLoader;
        private readonly ILogger<WorkflowController> _logger;
        private readonly IWfDefinitionService _defservice;
        private readonly IFormDataService _formDataservice;
        private readonly IWfExecLogService _execlogservice;
        private readonly IWfTaskService _taskService;
        private readonly IWorkflowActionService _workflowActionService;
        private readonly IWorkflowInstancePurger _workflowPurger;
        private readonly IWorkflowPersistenceProvider _store;
        private readonly IWfDbContext _workflowDb;

        public WorkflowController(IResolver resolver) : base(resolver)
        {
            _wfHost = resolver.Resolve<IWorkflowHost>();
            _workflowLoader = resolver.Resolve<IWorkflowLoader>();
            _defservice = resolver.Resolve<IWfDefinitionService>();
            _logger = resolver.GetLogger<WorkflowController>();
            _formDataservice = resolver.Resolve<IFormDataService>();
            _execlogservice = resolver.Resolve<IWfExecLogService>();
            _taskService = resolver.Resolve<IWfTaskService>();
            _workflowActionService = resolver.Resolve<IWorkflowActionService>();
            _workflowPurger = resolver.Resolve<IWorkflowInstancePurger>();
            _store = (IWorkflowPersistenceProvider)_wfHost.PersistenceStore;
            _workflowDb = resolver.Resolve<IWfDbContext>();
        }

        [HttpPost, Route("Load")]
        public IActionResult Load(LoadRequest request)
        {
            var def = _defservice.Query(x => x.ExternalId == request.WfDefinitionId && x.Version == request.Version).FirstOrDefault();
            if (def == null)
                return BadRequest($"审批流程定义({request.WfDefinitionId}:{request.Version})不存在");

            _workflowLoader.LoadDefinition(def);

            return ApiResult.Success(new { id = request.WfDefinitionId }).ToActionResult();
        }

        [HttpPost, Route("Start")]
        public async Task<IActionResult> StartAsync(StartRequest request)
        {
            if (IdentityContext.CurrentEmployee == null || (string.IsNullOrEmpty(request.WfDefinitionId) && string.IsNullOrEmpty(request.DataId)))
            {
                return BadRequest("发起人和流程定义Id和数据Id不能为空");
            }

            var formData = _formDataservice.Get(request.DataId);
            if (formData != null)
            {
                var cascade = request.EfCascade == CascadeMode.NotSet ? CascadeMode.All : request.EfCascade;
                // FormData.Id 来自 citext 主键，是该数据 ID 的规范大小写；工作流 Reference
                // 是 text，后续锁、唯一索引和查询都以此值统一，避免大小写变体产生多个实例。
                var dataId = formData.Id;
                var data = new WfDataContext(formData.CorpId ?? "", IdentityContext.CurrentUserID, IdentityContext.AccessToken, formData.AppId, formData.FormId, dataId, IdentityContext.CurrentEmployee.ToOperator(), cascade, request.EventIds);
                var version = request.Version;
                if (!request.Version.HasValue || request.Version.Value == 0)
                    version = _defservice.Find(request.WfDefinitionId)?.Version;

                // WorkflowCore 对未注册的流程定义会在 StartWorkflow 内抛异常，最终表现为 500。
                // 定义不存在/已禁用属调用方问题，提前判定并返回 400。
                var definition = _defservice.Find(request.WfDefinitionId, version);
                if (definition == null || definition.Disabled || definition.FlowType != FlowType.Workflow)
                {
                    return BadRequest($"审批流程定义({request.WfDefinitionId})不存在或未启用");
                }

                string wfinstId;
                string errMsg;

                // 同一条数据只允许存在一个在途实例。并发提交时两个请求都会查不到实例而各建一个，
                // 因此用 PG 咨询锁把「查询可复用实例 + 启动/重启」整段串行化。
                await _workflowDb.Database.OpenConnectionAsync();
                try
                {
                    await _workflowDb.Database.ExecuteSqlInterpolatedAsync($"select pg_advisory_lock(hashtextextended(lower({dataId}), 0::bigint))");
                    try
                    {
                        var existingInstance = ResolveReusableWorkflowInstance(dataId);
                        if (existingInstance != null)
                        {
                            wfinstId = existingInstance.Id;
                            errMsg = await RestartWorkflowInstanceAsync(existingInstance, data);
                        }
                        else
                        {
                            wfinstId = await _wfHost.StartWorkflow(request.WfDefinitionId, version, data.ToExpando(), dataId);
                            errMsg = WaitForComplete(wfinstId);
                        }
                    }
                    finally
                    {
                        await _workflowDb.Database.ExecuteSqlInterpolatedAsync($"select pg_advisory_unlock(hashtextextended(lower({dataId}), 0::bigint))");
                    }
                }
                finally
                {
                    _workflowDb.Database.CloseConnection();
                }

                if (!string.IsNullOrEmpty(errMsg))
                    return ApiResult.Fail(-1, errMsg, new { id = wfinstId }).ToActionResult();

                return ApiResult.Success(new { id = wfinstId }).ToActionResult();
            }

            return NotFound("数据不存在");
        }

        [HttpPost, Route("Approve")]
        public async Task<IActionResult> ApproveAsync(ApproveRequest request)
        {
            if (IdentityContext.CurrentEmployee == null || (string.IsNullOrEmpty(request.WfInstanceId) && string.IsNullOrEmpty(request.DataId))
                 || request.Action == ApproveAction.None)
            {
                return BadRequest("审批人和流程实例Id和数据Id不能为空");
            }

            var workerId = IdentityContext.CurrentEmployee.Id;
            var workerCode = IdentityContext.CurrentEmployee.Code;
            var task = _taskService.Query(x => x.DataId == request.DataId && x.EmployeeId == workerId)
                .FirstOrDefault(x => string.IsNullOrEmpty(request.WfNodeId) || x.ApproveNodeId == request.WfNodeId);
            if (task == null)
            {
                return BadRequest($"该员工({IdentityContext.CurrentEmployee.EmpName})没有审批权限");
            }

            request.WfNodeId = task.ApproveNodeId;
            request.WfInstanceId = string.IsNullOrEmpty(request.WfInstanceId) ? task.WfInstanceId : request.WfInstanceId;

            var wfInst = ResolveWorkflowInstance(request.WfInstanceId, request.DataId);
            if (wfInst == null)
            {
                return BadRequest("当前流程实例不可审批");
            }

            if (request.Action == ApproveAction.Approve)
            {
                await _workflowActionService.ValidateNodeActionEnabledAsync(wfInst, task, NodeActionType.Submit);
                await _workflowActionService.ValidateSubmitConditionAsync(wfInst, task);
            }
            else if (request.Action == ApproveAction.Reject)
            {
                await _workflowActionService.ValidateNodeActionEnabledAsync(wfInst, task, NodeActionType.Reject);
            }

            var act = await _wfHost.GetPendingActivity($"{request.WfInstanceId}_{request.DataId}_{request.WfNodeId}", workerId);
            if (act == null) return BadRequest($"指定数据/流程节点不可审批");

            var approveData = new WfApproveData(IdentityContext.CurrentCorpId, IdentityContext.CurrentUserID, workerId, workerCode, IdentityContext.CurrentEmployee.EmpName, request.Action, request.Comment, request.Signature, TsidIdGenerator.NewId());

            await _wfHost.SubmitActivitySuccess(act.Token, approveData.ToExpando());
            var errMsg = WaitForComplete(approveData.ExecLogId);

            if (!string.IsNullOrEmpty(errMsg))
                return ApiResult.Fail(-1, errMsg, new { id = request.WfInstanceId }).ToActionResult();

            return ApiResult.Success(new { id = request.WfInstanceId }).ToActionResult();
        }

        [HttpPost, Route("Submit")]
        public Task<IActionResult> SubmitAsync(ApproveRequest request)
        {
            request.Action = ApproveAction.Approve;
            return ApproveAsync(request);
        }

        [HttpPost, Route("Reject")]
        public Task<IActionResult> RejectAsync(ApproveRequest request)
        {
            request.Action = ApproveAction.Reject;
            return ApproveAsync(request);
        }

        [HttpPost, Route("Transfer")]
        public async Task<IActionResult> TransferAsync(TransferRequest request)
        {
            if (IdentityContext.CurrentEmployee == null || string.IsNullOrEmpty(request.DataId))
            {
                return BadRequest("审批人和数据Id不能为空");
            }

            var wfInst = ResolveWorkflowInstance(request.WfInstanceId, request.DataId);
            if (wfInst == null)
            {
                return BadRequest("当前流程实例不可转交");
            }

            var task = ResolveCurrentTask(request.DataId, request.WfNodeId);
            if (task == null)
            {
                return BadRequest($"该员工({IdentityContext.CurrentEmployee.EmpName})没有审批权限");
            }

            var result = await _workflowActionService.TransferAsync(new WorkflowActionDataContext
            {
                CorpId = IdentityContext.CurrentCorpId,
                CurrentEmployeeId = IdentityContext.CurrentEmployee.Id,
                CurrentEmployee = IdentityContext.CurrentEmployee.ToOperator(),
            }, wfInst, task, request.TargetEmployeeId, request.Comment);

            return ApiResult.Success(new { id = result.WorkflowInstanceId }).ToActionResult();
        }

        [HttpPost, Route("AddSign")]
        public async Task<IActionResult> AddSignAsync(AddSignRequest request)
        {
            if (IdentityContext.CurrentEmployee == null || string.IsNullOrEmpty(request.DataId))
            {
                return BadRequest("审批人和数据Id不能为空");
            }

            var wfInst = ResolveWorkflowInstance(request.WfInstanceId, request.DataId);
            if (wfInst == null)
            {
                return BadRequest("当前流程实例不可加签");
            }

            var task = ResolveCurrentTask(request.DataId, request.WfNodeId);
            if (task == null)
            {
                return BadRequest($"该员工({IdentityContext.CurrentEmployee.EmpName})没有审批权限");
            }

            var result = await _workflowActionService.AddSignAsync(new WorkflowActionDataContext
            {
                CorpId = IdentityContext.CurrentCorpId,
                CurrentEmployeeId = IdentityContext.CurrentEmployee.Id,
                CurrentEmployee = IdentityContext.CurrentEmployee.ToOperator(),
            }, wfInst, task, request.TargetEmployeeId, request.Comment);

            return ApiResult.Success(new { id = result.WorkflowInstanceId }).ToActionResult();
        }

        [HttpPost, Route("Return")]
        public async Task<IActionResult> ReturnAsync(ReturnRequest request)
        {
            if (IdentityContext.CurrentEmployee == null || string.IsNullOrEmpty(request.DataId))
            {
                return BadRequest("审批人和数据Id不能为空");
            }

            var wfInst = ResolveWorkflowInstance(request.WfInstanceId, request.DataId);
            if (wfInst == null)
            {
                return BadRequest("当前流程实例不可回退");
            }

            var task = ResolveCurrentTask(request.DataId, request.WfNodeId);
            if (task == null)
            {
                return BadRequest($"该员工({IdentityContext.CurrentEmployee.EmpName})没有审批权限");
            }

            var result = await _workflowActionService.ReturnAsync(new WorkflowActionDataContext
            {
                CorpId = IdentityContext.CurrentCorpId,
                CurrentEmployeeId = IdentityContext.CurrentEmployee.Id,
                CurrentEmployee = IdentityContext.CurrentEmployee.ToOperator(),
            }, wfInst, task, request.TargetNodeId, request.Comment);

            return ApiResult.Success(new { id = result.WorkflowInstanceId }).ToActionResult();
        }

        [HttpPost, Route("Withdraw")]
        public async Task<IActionResult> WithdrawAsync(WithdrawRequest request)
        {
            if (IdentityContext.CurrentEmployee == null || string.IsNullOrEmpty(request.DataId))
            {
                return BadRequest("发起人和数据Id不能为空");
            }

            var wfInst = ResolveWorkflowInstance(request.WfInstanceId, request.DataId);
            if (wfInst == null)
            {
                return BadRequest("当前流程实例不可撤回");
            }

            var task = ResolveCurrentTask(request.DataId, string.Empty);
            if (task == null || !string.Equals(task.WfInstanceId, wfInst.Id, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("当前流程无可操作待办");
            }
            if (task?.Starter?.Id != IdentityContext.CurrentEmployee.Id)
            {
                return BadRequest("仅流程发起人可撤回");
            }

            var definition = _defservice.Query(x => x.ExternalId == wfInst.WorkflowDefinitionId && x.Version == wfInst.Version).FirstOrDefault();
            var withdrawRule = definition?.Metadata?.WorkflowSetting?.WithdrawRule ?? WorkflowWithdrawRule.Disabled;
            if (withdrawRule == WorkflowWithdrawRule.Disabled)
            {
                return BadRequest("当前流程不允许撤回");
            }

            if (withdrawRule == WorkflowWithdrawRule.StarterOnly)
            {
                var firstApproveNodeId = definition?.Metadata?.Steps?.FirstOrDefault(x => x.NodeType == WfNodeType.Approve)?.Id;
                if (!string.IsNullOrWhiteSpace(firstApproveNodeId) && task?.ApproveNodeId != firstApproveNodeId)
                {
                    return BadRequest("当前节点不允许撤回");
                }
            }

            var formDef = Resolver.GetRepository<FormDef>().Get(task.FormId);
            var result = await _workflowActionService.WithdrawAsync(
                new WorkflowActionDataContext
                {
                    CorpId = IdentityContext.CurrentCorpId,
                    CurrentEmployeeId = IdentityContext.CurrentEmployee.Id,
                    CurrentEmployee = IdentityContext.CurrentEmployee.ToOperator(),
                },
                wfInst,
                task,
                formDef?.Name ?? string.Empty,
                request.Comment);

            // Clear runtime artifacts as part of withdrawal so a later resubmission
            // starts with a clean workflow runtime.
            await _store.ClearWorkflowRuntime(wfInst.Id);

            return ApiResult.Success(new { id = result.WorkflowInstanceId }).ToActionResult();
        }

        [HttpPost, Route("Urge")]
        public async Task<IActionResult> UrgeAsync(UrgeRequest request)
        {
            if (IdentityContext.CurrentEmployee == null || string.IsNullOrEmpty(request.DataId))
            {
                return BadRequest("发起人和数据Id不能为空");
            }

            var wfInst = ResolveWorkflowInstance(request.WfInstanceId, request.DataId);
            if (wfInst == null)
            {
                return BadRequest("当前流程实例不可催办");
            }

            var task = ResolveCurrentTask(request.DataId, string.Empty);
            if (task == null || !string.Equals(task.WfInstanceId, wfInst.Id, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("当前流程无可操作待办");
            }
            if (task?.Starter?.Id != IdentityContext.CurrentEmployee.Id)
            {
                return BadRequest("仅流程发起人可催办");
            }

            var definition = _defservice.Query(x => x.ExternalId == wfInst.WorkflowDefinitionId && x.Version == wfInst.Version).FirstOrDefault();
            if (definition?.Metadata?.WorkflowSetting?.AllowUrge != true)
            {
                return BadRequest("当前流程不允许催办");
            }

            var result = await _workflowActionService.UrgeAsync(
                new WorkflowActionDataContext
                {
                    CorpId = IdentityContext.CurrentCorpId,
                    CurrentEmployeeId = IdentityContext.CurrentEmployee.Id,
                    CurrentEmployee = IdentityContext.CurrentEmployee.ToOperator(),
                },
                wfInst,
                task,
                request.DataId);

            return ApiResult.Success(new { id = result.WorkflowInstanceId }).ToActionResult();
        }

        [HttpGet, Route("ActionStatus")]
        public IActionResult GetActionStatus([FromQuery] ActionStatusRequest request)
        {
            if (IdentityContext.CurrentEmployee == null || string.IsNullOrEmpty(request.DataId))
            {
                return BadRequest("发起人和数据Id不能为空");
            }

            var wfInst = ResolveWorkflowInstance(request.WfInstanceId, request.DataId);
            if (wfInst == null)
            {
                return Ok(new WorkflowActionStatusResponse());
            }

            var task = ResolveCurrentTask(request.DataId, string.Empty);
            if (task == null || !string.Equals(task.WfInstanceId, wfInst.Id, StringComparison.OrdinalIgnoreCase))
            {
                return Ok(new WorkflowActionStatusResponse());
            }
            var definition = _defservice.Query(x => x.ExternalId == wfInst.WorkflowDefinitionId && x.Version == wfInst.Version).FirstOrDefault();
            var status = _workflowActionService.GetActionStatus(IdentityContext.CurrentEmployee.Id, task, definition);

            return Ok(new WorkflowActionStatusResponse
            {
                CanUrge = status.CanUrge,
                CanWithdraw = status.CanWithdraw
            });
        }

        [HttpGet, Route("NodeActions")]
        public IActionResult GetNodeActions([FromQuery] ActionStatusRequest request)
        {
            if (IdentityContext.CurrentEmployee == null || string.IsNullOrEmpty(request.DataId))
            {
                return BadRequest("审批人和数据Id不能为空");
            }

            var task = ResolveCurrentTask(request.DataId, string.Empty);
            var wfInst = ResolveWorkflowInstance(request.WfInstanceId, request.DataId);
            if (task == null || wfInst == null || !string.Equals(task.WfInstanceId, wfInst.Id, StringComparison.OrdinalIgnoreCase))
            {
                return Ok(new List<NodeActionResponse>());
            }

            var definition = _defservice.Query(x =>
                    x.CorpId == IdentityContext.CurrentCorpId &&
                    x.ExternalId == wfInst.WorkflowDefinitionId &&
                    x.Version == wfInst.Version)
                .FirstOrDefault();
            var actions = definition?.Metadata?.Steps
                .FirstOrDefault(x => x.Id == task.ApproveNodeId)?
                .WfNodeSetting?.ApproveSetting?.NodeActions;

            return Ok(actions?.Select(action => new NodeActionResponse
            {
                ActionType = action.ActionType.ToString().ToLowerInvariant(),
                Enabled = action.Enabled,
                Text = action.Text,
                Candidates = action.Candidates?.Select(candidate => new ApprovalCandidateResponse
                {
                    CandidateId = candidate.CandidateId,
                    CandidateType = (int)candidate.CandidateType,
                    CandidateName = candidate.CandidateName,
                    CascadedDept = candidate.CascadedDept,
                }).ToList(),
            }).ToList() ?? []);
        }

        [HttpGet, Route("ReturnNodes")]
        public async Task<IActionResult> ReturnNodesAsync([FromQuery] ActionStatusRequest request)
        {
            if (IdentityContext.CurrentEmployee == null || string.IsNullOrEmpty(request.DataId))
            {
                return BadRequest("审批人和数据Id不能为空");
            }

            var wfInst = ResolveWorkflowInstance(request.WfInstanceId, request.DataId);
            if (wfInst == null)
            {
                return Ok(new List<ReturnTargetNode>());
            }

            var task = ResolveCurrentTask(request.DataId, string.Empty);
            if (task == null)
            {
                return Ok(new List<ReturnTargetNode>());
            }

            var targets = await _workflowActionService.GetReturnNodesAsync(new WorkflowActionDataContext
            {
                CorpId = IdentityContext.CurrentCorpId,
                CurrentEmployeeId = IdentityContext.CurrentEmployee.Id,
                CurrentEmployee = IdentityContext.CurrentEmployee.ToOperator(),
            }, wfInst, task);

            return Ok(targets.Select(x => new ReturnTargetNode
            {
                NodeId = x.NodeId,
                NodeName = x.NodeName,
                Round = x.Round,
            }).ToList());
        }

        [HttpPost, Route("Terminate")]
        public async Task<IActionResult> TerminateAsync(TerminateRequest request)
        {
            if (IdentityContext.CurrentEmployee == null || (string.IsNullOrEmpty(request.WfInstanceId) && string.IsNullOrEmpty(request.DataId)))
            {
                return BadRequest("审批人和流程实例Id和数据Id不能为空");
            }

            if (IdentityContext.IdentityType != ApiService.IdentityType.CorpAdmin)
            {
                return Forbid();
            }

            WorkflowInstance? wfInst;
            if (!string.IsNullOrEmpty(request.WfInstanceId))
            {
                wfInst = _workflowDb.WorkflowInstances.Where(x => x.Id == request.WfInstanceId && x.Status == WorkflowStatus.Runnable).FirstOrDefault();
            }
            else
            {
                var normalizedDataId = request.DataId.ToLowerInvariant();
                wfInst = _workflowDb.WorkflowInstances
                    .Where(x => x.Reference.ToLower() == normalizedDataId && x.Status == WorkflowStatus.Runnable)
                    .FirstOrDefault();
            }

            if (wfInst != null)
            {
                var result = await _wfHost.TerminateWorkflow(wfInst.Id);

                if (!result)
                    return ApiResult.Fail(-1, "指定数程实例中止失败", new { id = request.WfInstanceId }).ToActionResult();

                _taskService.Delete(new DynamicFilter
                {
                    Rel = "and",
                    Items = [new DynamicFilter { Field = "WfInstanceId", Op = FilterOp.Eq, Value = wfInst.Id }]
                });
            }

            return ApiResult.Success(new { id = request.WfInstanceId }).ToActionResult();
        }

        [HttpPost, Route("Instance/Delete")]
        public async Task<IActionResult> DeleteInstancesAsync(DeleteWorkflowInstancesRequest? request, CancellationToken cancellationToken)
        {
            var workflowInstanceIds = await _workflowPurger.DeleteWorkflowInstancesAsync(
                request?.DataIds,
                request?.WfInstanceIds,
                cancellationToken);

            return ApiResult.Success(new
            {
                id = string.Join(",", workflowInstanceIds),
                error = string.Empty
            }).ToActionResult();
        }

        [HttpPost, Route("ChangeApprover")]
        public async Task<IActionResult> ChangeApproverAsync(ChangeApproverRequest request)
        {
            if (IdentityContext.CurrentEmployee == null || string.IsNullOrEmpty(request.DataId) || string.IsNullOrEmpty(request.TargetEmployeeId))
            {
                return BadRequest("审批人、数据Id和目标审批人不能为空");
            }

            if (IdentityContext.IdentityType != ApiService.IdentityType.CorpAdmin)
            {
                return BadRequest($"该员工({IdentityContext.CurrentEmployee.EmpName})没有变更审批人权限");
            }

            var wfInst = ResolveWorkflowInstance(request.WfInstanceId, request.DataId);
            if (wfInst == null)
            {
                return BadRequest("当前流程实例不可变更审批人");
            }

            var tasks = _taskService.Query(x => x.CorpId == IdentityContext.CurrentCorpId
                && x.DataId == request.DataId
                && x.WfInstanceId == wfInst.Id);
            if (!string.IsNullOrEmpty(request.WfNodeId))
            {
                tasks = tasks.Where(x => x.ApproveNodeId == request.WfNodeId);
            }

            var task = tasks.FirstOrDefault();
            if (task == null)
            {
                return BadRequest("当前节点待办不存在");
            }

            var result = await _workflowActionService.ChangeApproverAsync(new WorkflowActionDataContext
            {
                CorpId = IdentityContext.CurrentCorpId,
                CurrentEmployeeId = IdentityContext.CurrentEmployee.Id,
                CurrentEmployee = IdentityContext.CurrentEmployee.ToOperator(),
            }, wfInst, task, request.TargetEmployeeId, request.Comment);

            return ApiResult.Success(new { id = result.WorkflowInstanceId }).ToActionResult();
        }

        [HttpPost, Route("ExpireAction")]
        public async Task<IActionResult> ExpireActionAsync(ExpireActionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.WfInstanceId) || string.IsNullOrWhiteSpace(request.DataId) || string.IsNullOrWhiteSpace(request.WfNodeId))
            {
                return BadRequest("流程实例Id、数据Id和节点Id不能为空");
            }

            var wfInst = ResolveWorkflowInstance(request.WfInstanceId, request.DataId);
            if (wfInst == null)
            {
                return BadRequest("当前流程实例不可执行超时动作");
            }

            var task = _taskService.Query(x => x.WfInstanceId == request.WfInstanceId && x.DataId == request.DataId && x.ApproveNodeId == request.WfNodeId).FirstOrDefault();
            if (task == null)
            {
                return BadRequest("当前节点待办不存在");
            }

            var result = await _workflowActionService.HandleExpiredTaskAsync(wfInst, task);
            return ApiResult.Success(new { id = result.WorkflowInstanceId }).ToActionResult();
        }

        [HttpGet, Route("Status")]
        public async Task<IActionResult> GetStatusAsync([FromQuery] StatusRequest request)
        {
            if (string.IsNullOrEmpty(request.WfInstanceId))
            {
                return BadRequest("流程实例Id不能为空");
            }

            var wf = await _wfHost.PersistenceStore.GetWorkflowInstance(request.WfInstanceId);
            return Ok(new { id = request.WfInstanceId, status = wf.Status.ToString() });
        }

        private string WaitForComplete(string exeLogId)
        {
            var timeOut = TimeSpan.FromSeconds(1);
            var execLog = _execlogservice.Get(exeLogId);

            int num = 0;
            while (execLog == null && (double)num < timeOut.TotalMilliseconds / 100.0)
            {
                Thread.Sleep(200);
                num++;
                execLog = _execlogservice.Get(exeLogId);
            }

            if (execLog == null || execLog.Success)
                return string.Empty;

            return execLog.ErrMsg;
        }

        private WorkflowInstance? ResolveWorkflowInstance(string? wfInstanceId, string dataId)
        {
            if (!string.IsNullOrEmpty(wfInstanceId))
            {
                return _workflowDb.WorkflowInstances.FirstOrDefault(x => x.Id == wfInstanceId && x.Status == WorkflowStatus.Runnable);
            }

            var normalizedDataId = dataId.ToLowerInvariant();
            return _workflowDb.WorkflowInstances.FirstOrDefault(x => x.Reference.ToLower() == normalizedDataId && x.Status == WorkflowStatus.Runnable);
        }

        private Wf_Task? ResolveCurrentTask(string dataId, string? wfNodeId)
        {
            var workerId = IdentityContext.CurrentEmployee?.Id;
            if (string.IsNullOrWhiteSpace(workerId))
            {
                return null;
            }

            return _taskService.Query(x => x.DataId == dataId && x.EmployeeId == workerId)
                .FirstOrDefault(x => string.IsNullOrEmpty(wfNodeId) || x.ApproveNodeId == wfNodeId);
        }

        private WorkflowInstance? ResolveReusableWorkflowInstance(string dataId)
        {
            // 在途（Runnable）与挂起（Suspended）都算可复用：重新提交时重置同一实例，
            // 而不是再建一个实例（同一条数据只允许一个活跃实例）。
            var normalizedDataId = dataId.ToLowerInvariant();
            return _workflowDb.WorkflowInstances
                .Where(x => x.Reference.ToLower() == normalizedDataId
                            && (x.Status == WorkflowStatus.Suspended || x.Status == WorkflowStatus.Runnable))
                .OrderByDescending(x => x.CreateTime)
                .FirstOrDefault();
        }

        private async Task<string> RestartWorkflowInstanceAsync(WorkflowInstance wfInst, WfDataContext data)
        {
            var existingData = WfDataContext.FromData((IDictionary<string, object?>)wfInst.Data);
            var restartData = new WfDataContext(
                data.CorpId,
                data.UserId,
                data.AccessToken,
                data.AppId,
                data.FormId,
                data.DataId,
                data.WfStarter,
                data.EfCascade,
                data.EventIds)
            {
                Round = existingData.Round + 1
            };

            wfInst.Data = restartData.ToExpando();

            // 指针重置后节点会重新生成待办，在途的旧待办必须先清掉，否则同一节点会留下两份待办。
            _taskService.Delete(new DynamicFilter
            {
                Rel = "and",
                Items = [new DynamicFilter { Field = "WfInstanceId", Op = FilterOp.Eq, Value = wfInst.Id }]
            });

            // 指针必须先重置到发起节点：持久化时按传入的指针集合差量同步，而
            // ExecutionPointers 导航在 EF 里被 Ignore（重载后为空），空集合会把旧指针
            // 全部删除，实例变成「Runnable 但无待执行指针」并被引擎直接判为 Complete。
            _workflowActionService.ResetToStart(wfInst);
            wfInst.Status = WorkflowStatus.Runnable;
            wfInst.NextExecution = 0;
            wfInst.CompleteTime = null;

            await _store.PersistWorkflow(wfInst);
            return WaitForComplete(wfInst.Id);
        }


        [HttpPost, Route("Definition/Delete")]
        public async Task<IActionResult> DeleteDef(DeleteRequest request)
        {
            var defs = (string.IsNullOrEmpty(request.AppId)
                ? _defservice.Query(x => x.FlowType == FlowType.Workflow && request.FormIds!.Contains(x.SourceId!) && !x.DeleteFlag)
                : _defservice.Query(x => x.AppId == request.AppId && !x.DeleteFlag))
                .Select(x => new { x.Id, x.ExternalId })
                .ToList();
            var defIds = defs.Select(x => x.Id).ToList();

            if (defIds.Count > 0)
            {
                // WorkflowInstance.WorkflowDefinitionId 存的是 ExternalId，不是定义内部 Id。
                var externalIds = defs.Select(x => x.ExternalId).Distinct().ToList();
                var wfInstIds = _workflowDb.WorkflowInstances.Where(x => externalIds.Contains(x.WorkflowDefinitionId) && x.Status == WorkflowStatus.Runnable).Select(x => x.Id).ToList();
                var terminateResults = await Task.WhenAll(wfInstIds.Select(async id => new
                {
                    Id = id,
                    Success = await _wfHost.TerminateWorkflow(id)
                }));

                var failedIds = terminateResults.Where(x => !x.Success).Select(x => x.Id).ToList();
                if (failedIds.Count > 0)
                {
                    return ApiResult.Fail(-1, "审批流程实例中止失败", new { ids = failedIds }).ToActionResult();
                }

                // 与 Terminate 端点一致：实例终止后待办必须一并清掉，否则会留在「我的待办」里
                // 指向一个已经不存在的流程定义。
                foreach (var id in wfInstIds)
                {
                    _taskService.Delete(new DynamicFilter
                    {
                        Rel = "and",
                        Items = [new DynamicFilter { Field = "WfInstanceId", Op = FilterOp.Eq, Value = id }]
                    });
                }
            }

            if (request.DeleteDef.HasValue && request.DeleteDef.Value && defIds?.Count > 0)
            {
                // 走到这里说明应用/表单已被删除，其下定义（含已发布版本）必须一并清理，
                // 否则会残留永远删不掉的孤儿定义。
                await _defservice.DeleteForceAsync(defIds);
            }

            return Ok();
        }

#if DEBUG
        //有些方法将写在API中，此处为调试用
        [HttpPost, Route("Definition/Create")]
        public IActionResult Create(CreateRequest request)
        {
            var def = new Wf_Definition()
            {
                Description = request.Description,
                Version = request.Version,
                Content = "",
                ExternalId = request.WfDefinitionId,
            };

            var exist = _defservice.Query(x => x.ExternalId == def.ExternalId && x.Version == def.Version).FirstOrDefault();
            if (exist != null)
            {
                def.Id = exist.Id;
                _defservice.Replace(def);
            }
            else
            {
                _defservice.Add(def);
            }

            var defId = def.Id;
            def = _defservice.Get(defId);
            _workflowLoader.LoadDefinition(def!);

            return Ok(defId);
        }

        [HttpGet, Route("Definition")]
        public IActionResult GetDefinition([FromQuery] CreateRequest request)
        {
            return Ok(_defservice.Query(x => x.ExternalId == request.WfDefinitionId).ToList());
        }

        [HttpGet, Route("Instance")]
        public async Task<IActionResult> GetInstance([FromQuery] StatusRequest request)
        {
            if (string.IsNullOrEmpty(request.WfInstanceId) && string.IsNullOrEmpty(request.DataId))
            {
                return BadRequest();
            }

            var wf = await _wfHost.PersistenceStore.GetWorkflowInstance(request.WfInstanceId);
            return Ok(wf);
        }
#endif
    }
#if DEBUG
    public class CreateRequest()
    {
        public string WfDefinitionId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Version { get; set; }
        public string Content { get; set; } = string.Empty;
    }
#endif

    public class LoadRequest()
    {
        public string WfDefinitionId { get; set; } = string.Empty;
        public int Version { get; set; }
    }

    public class StartRequest
    {
        public string WfDefinitionId { get; set; } = string.Empty;
        public int? Version { get; set; }
        public string DataId { get; set; } = string.Empty;
        public CascadeMode EfCascade { get; set; }
        public string? EventIds { get; set; }
    }
    public class ApproveRequest
    {
        public string WfInstanceId { get; set; } = string.Empty;
        public string DataId { get; set; } = string.Empty;
        public string WfNodeId { get; set; } = string.Empty;
        public string WorkerId { get; set; } = string.Empty;
        public ApproveAction Action { get; set; }
        public string Comment { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
        public string TargetNodeId { get; set; } = string.Empty;
        public string TargetEmployeeId { get; set; } = string.Empty;
    }

    public class ReturnRequest
    {
        public string WfInstanceId { get; set; } = string.Empty;
        public string DataId { get; set; } = string.Empty;
        public string WfNodeId { get; set; } = string.Empty;
        public string TargetNodeId { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
    }

    public class AddSignRequest
    {
        public string WfInstanceId { get; set; } = string.Empty;
        public string DataId { get; set; } = string.Empty;
        public string WfNodeId { get; set; } = string.Empty;
        public string TargetEmployeeId { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
    }

    public class TransferRequest
    {
        public string WfInstanceId { get; set; } = string.Empty;
        public string DataId { get; set; } = string.Empty;
        public string WfNodeId { get; set; } = string.Empty;
        public string TargetEmployeeId { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
    }

    public class WithdrawRequest
    {
        public string WfInstanceId { get; set; } = string.Empty;
        public string DataId { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
    }

    public class UrgeRequest
    {
        public string WfInstanceId { get; set; } = string.Empty;
        public string DataId { get; set; } = string.Empty;
    }

    public class ActionStatusRequest
    {
        public string WfInstanceId { get; set; } = string.Empty;
        public string DataId { get; set; } = string.Empty;
    }

    public class WorkflowActionStatusResponse
    {
        public bool CanWithdraw { get; set; }
        public bool CanUrge { get; set; }
    }

    public class NodeActionResponse
    {
        public string ActionType { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public string? Text { get; set; }
        public List<ApprovalCandidateResponse>? Candidates { get; set; }
    }

    public class ApprovalCandidateResponse
    {
        public string CandidateId { get; set; } = string.Empty;
        public int CandidateType { get; set; }
        public string? CandidateName { get; set; }
        public bool CascadedDept { get; set; }
    }

    public class ReturnTargetNode
    {
        public string NodeId { get; set; } = string.Empty;
        public string NodeName { get; set; } = string.Empty;
        public int Round { get; set; }
    }

    public class TerminateRequest
    {
        public string WfInstanceId { get; set; } = string.Empty;
        public string DataId { get; set; } = string.Empty;
        public string WorkerId { get; set; } = string.Empty;
    }

    public class StatusRequest
    {
        public string WfInstanceId { get; set; } = string.Empty;
        public string DataId { get; set; } = string.Empty;
    }
    public class DeleteRequest()
    {
        public string? AppId { get; set; }
        public List<string>? FormIds { get; set; }
        public bool? DeleteDef { get; set; }
    }
    public class DeleteWorkflowInstancesRequest
    {
        public IEnumerable<string>? DataIds { get; set; }
        public IEnumerable<string>? WfInstanceIds { get; set; }
    }
    public class ChangeApproverRequest
    {
        public string WfInstanceId { get; set; } = string.Empty;
        public string DataId { get; set; } = string.Empty;
        public string WfNodeId { get; set; } = string.Empty;
        public string TargetEmployeeId { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
    }

    public class ExpireActionRequest
    {
        public string WfInstanceId { get; set; } = string.Empty;
        public string DataId { get; set; } = string.Empty;
        public string WfNodeId { get; set; } = string.Empty;
        public WfExpireActionType ActionType { get; set; }
    }
}
