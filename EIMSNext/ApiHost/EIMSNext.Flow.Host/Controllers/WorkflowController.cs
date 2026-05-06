using Asp.Versioning;

using System.Dynamic;
using System.Linq;

using EIMSNext.ApiHost.Controllers;
using EIMSNext.ApiHost.Extensions;
using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Core;
using EIMSNext.Core.Query;
using EIMSNext.Service.Entities;
using EIMSNext.Flow.Core;
using EIMSNext.Flow.Core.Interfaces;
using EIMSNext.Flow.Persistence;
using EIMSNext.Service.Contracts;

using HKH.Mef2.Integration;

using Microsoft.AspNetCore.Mvc;

using MongoDB.Driver;

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
        private readonly IWfTodoService _todoservice;
        private readonly IWorkflowActionService _workflowActionService;
        private readonly IMongoPersistenceProvider _store;

        public WorkflowController(IResolver resolver) : base(resolver)
        {
            _wfHost = resolver.Resolve<IWorkflowHost>();
            _workflowLoader = resolver.Resolve<IWorkflowLoader>();
            _defservice = resolver.Resolve<IWfDefinitionService>();
            _logger = resolver.GetLogger<WorkflowController>();
            _formDataservice = resolver.Resolve<IFormDataService>();
            _execlogservice = resolver.Resolve<IWfExecLogService>();
            _todoservice = resolver.Resolve<IWfTodoService>();
            _workflowActionService = resolver.Resolve<IWorkflowActionService>();
            _store = (IMongoPersistenceProvider)_wfHost.PersistenceStore;
        }

        [HttpPost, Route("Load")]
        public IActionResult Load(LoadRequest request)
        {
            var def = _defservice.Find(x => x.ExternalId == request.WfDefinitionId && x.Version == request.Version).FirstOrDefault();
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
                var data = new WfDataContext(formData.CorpId ?? "", IdentityContext.CurrentUserID, IdentityContext.AccessToken, formData.AppId, formData.FormId, request.DataId, IdentityContext.CurrentEmployee.ToOperator(), CascadeMode.All, null);
                var version = request.Version;
                if (!request.Version.HasValue || request.Version.Value == 0)
                    version = _defservice.Find(request.WfDefinitionId)?.Version;

                var existingInstance = ResolveReusableWorkflowInstance(request.DataId);
                string wfinstId;
                string errMsg;
                if (existingInstance != null)
                {
                    wfinstId = existingInstance.Id;
                    errMsg = await RestartWorkflowInstanceAsync(existingInstance, data);
                }
                else
                {
                    wfinstId = await _wfHost.StartWorkflow(request.WfDefinitionId, version, data.ToExpando(), request.DataId);
                    errMsg = WaitForComplete(wfinstId);
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
            var todo = _todoservice.Find(x => x.DataId == request.DataId && x.EmployeeId == workerId)
                .ToList()
                .FirstOrDefault(x => string.IsNullOrEmpty(request.WfNodeId) || x.ApproveNodeId == request.WfNodeId);
            if (todo == null)
            {
                return BadRequest($"该员工({IdentityContext.CurrentEmployee.EmpName})没有审批权限");
            }

            request.WfNodeId = todo.ApproveNodeId;
            request.WfInstanceId = string.IsNullOrEmpty(request.WfInstanceId) ? todo.WfInstanceId : request.WfInstanceId;

            var act = await _wfHost.GetPendingActivity($"{request.WfInstanceId}_{request.DataId}_{request.WfNodeId}", workerId);
            if (act == null) return BadRequest($"指定数据/流程节点不可审批");

            var approveData = new WfApproveData(IdentityContext.CurrentCorpId, IdentityContext.CurrentUserID, IdentityContext.CurrentUserID, workerId, IdentityContext.CurrentEmployee.EmpName, request.Action, request.Comment, request.Signature, Guid.NewGuid().ToString());

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

            var todo = ResolveCurrentTodo(request.DataId, request.WfNodeId);
            if (todo == null)
            {
                return BadRequest($"该员工({IdentityContext.CurrentEmployee.EmpName})没有审批权限");
            }

            var result = await _workflowActionService.TransferAsync(new WorkflowActionDataContext
            {
                CorpId = IdentityContext.CurrentCorpId,
                CurrentEmployeeId = IdentityContext.CurrentEmployee.Id,
                CurrentEmployee = IdentityContext.CurrentEmployee.ToOperator(),
            }, wfInst, todo, request.TargetEmployeeId, request.Comment);

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

            var todo = ResolveCurrentTodo(request.DataId, request.WfNodeId);
            if (todo == null)
            {
                return BadRequest($"该员工({IdentityContext.CurrentEmployee.EmpName})没有审批权限");
            }

            var result = await _workflowActionService.AddSignAsync(new WorkflowActionDataContext
            {
                CorpId = IdentityContext.CurrentCorpId,
                CurrentEmployeeId = IdentityContext.CurrentEmployee.Id,
                CurrentEmployee = IdentityContext.CurrentEmployee.ToOperator(),
            }, wfInst, todo, request.TargetEmployeeId, request.Comment);

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

            var todo = ResolveCurrentTodo(request.DataId, request.WfNodeId);
            if (todo == null)
            {
                return BadRequest($"该员工({IdentityContext.CurrentEmployee.EmpName})没有审批权限");
            }

            var result = await _workflowActionService.ReturnAsync(new WorkflowActionDataContext
            {
                CorpId = IdentityContext.CurrentCorpId,
                CurrentEmployeeId = IdentityContext.CurrentEmployee.Id,
                CurrentEmployee = IdentityContext.CurrentEmployee.ToOperator(),
            }, wfInst, todo, request.TargetNodeId, request.Comment);

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

            var todo = _todoservice.Find(x => x.WfInstanceId == wfInst.Id).FirstOrDefault();
            if (todo?.Starter?.Id != IdentityContext.CurrentEmployee.Id)
            {
                return BadRequest("仅流程发起人可撤回");
            }

            var definition = _defservice.Find(x => x.ExternalId == wfInst.WorkflowDefinitionId && x.Version == wfInst.Version).FirstOrDefault();
            var withdrawRule = definition?.Metadata?.WorkflowSetting?.WithdrawRule ?? WorkflowWithdrawRule.Disabled;
            if (withdrawRule == WorkflowWithdrawRule.Disabled)
            {
                return BadRequest("当前流程不允许撤回");
            }

            if (withdrawRule == WorkflowWithdrawRule.StarterOnly)
            {
                var firstApproveNodeId = definition?.Metadata?.Steps?.FirstOrDefault(x => x.NodeType == WfNodeType.Approve)?.Id;
                if (!string.IsNullOrWhiteSpace(firstApproveNodeId) && todo?.ApproveNodeId != firstApproveNodeId)
                {
                    return BadRequest("当前节点不允许撤回");
                }
            }

            var formDef = Resolver.GetRepository<FormDef>().Get(todo.FormId);
            var result = await _workflowActionService.WithdrawAsync(
                new WorkflowActionDataContext
                {
                    CorpId = IdentityContext.CurrentCorpId,
                    CurrentEmployeeId = IdentityContext.CurrentEmployee.Id,
                    CurrentEmployee = IdentityContext.CurrentEmployee.ToOperator(),
                },
                wfInst,
                todo,
                formDef?.Name ?? string.Empty,
                request.Comment);

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

            var todo = _todoservice.Find(x => x.WfInstanceId == wfInst.Id).FirstOrDefault();
            if (todo?.Starter?.Id != IdentityContext.CurrentEmployee.Id)
            {
                return BadRequest("仅流程发起人可催办");
            }

            var definition = _defservice.Find(x => x.ExternalId == wfInst.WorkflowDefinitionId && x.Version == wfInst.Version).FirstOrDefault();
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
                todo,
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

            var todo = _todoservice.Find(x => x.WfInstanceId == wfInst.Id).FirstOrDefault();
            var definition = _defservice.Find(x => x.ExternalId == wfInst.WorkflowDefinitionId && x.Version == wfInst.Version).FirstOrDefault();
            var status = _workflowActionService.GetActionStatus(IdentityContext.CurrentEmployee.Id, todo, definition);

            return Ok(new WorkflowActionStatusResponse
            {
                CanUrge = status.CanUrge,
                CanWithdraw = status.CanWithdraw
            });
        }

        [HttpGet, Route("ReturnTargets")]
        public async Task<IActionResult> ReturnTargetsAsync([FromQuery] ActionStatusRequest request)
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

            var todo = ResolveCurrentTodo(request.DataId, string.Empty);
            if (todo == null)
            {
                return Ok(new List<ReturnTargetNode>());
            }

            var targets = await _workflowActionService.GetReturnTargetsAsync(new WorkflowActionDataContext
            {
                CorpId = IdentityContext.CurrentCorpId,
                CurrentEmployeeId = IdentityContext.CurrentEmployee.Id,
                CurrentEmployee = IdentityContext.CurrentEmployee.ToOperator(),
            }, wfInst, todo);

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
                return BadRequest($"该员工({IdentityContext.CurrentEmployee.EmpName})没有中止权限");
            }

            WorkflowInstance? wfInst;
            if (!string.IsNullOrEmpty(request.WfInstanceId))
                wfInst = _store.GetWorkflowInstances().Where(x => x.Id == request.WfInstanceId && x.Status == WorkflowStatus.Runnable).FirstOrDefault();
            else
                wfInst = _store.GetWorkflowInstances().Where(x => x.Reference == request.DataId && x.Status == WorkflowStatus.Runnable).FirstOrDefault();

            if (wfInst != null)
            {
                var result = await _wfHost.TerminateWorkflow(wfInst.Id);

                if (!result)
                    return ApiResult.Fail(-1, "指定数程实例中止失败", new { id = request.WfInstanceId }).ToActionResult();
            }

            return ApiResult.Success(new { id = request.WfInstanceId }).ToActionResult();
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
                return _store.GetWorkflowInstances().FirstOrDefault(x => x.Id == wfInstanceId && x.Status == WorkflowStatus.Runnable);
            }

            return _store.GetWorkflowInstances().FirstOrDefault(x => x.Reference == dataId && x.Status == WorkflowStatus.Runnable);
        }

        private Wf_Todo? ResolveCurrentTodo(string dataId, string? wfNodeId)
        {
            var workerId = IdentityContext.CurrentEmployee?.Id;
            if (string.IsNullOrWhiteSpace(workerId))
            {
                return null;
            }

            return _todoservice.Find(x => x.DataId == dataId && x.EmployeeId == workerId)
                .ToList()
                .FirstOrDefault(x => string.IsNullOrEmpty(wfNodeId) || x.ApproveNodeId == wfNodeId);
        }

        private WorkflowInstance? ResolveReusableWorkflowInstance(string dataId)
        {
            return _store.GetWorkflowInstances()
                .Where(x => x.Reference == dataId && x.Status == WorkflowStatus.Suspended)
                .OrderByDescending(x => x.CreateTime)
                .FirstOrDefault();
        }

        private async Task<string> RestartWorkflowInstanceAsync(WorkflowInstance wfInst, WfDataContext data)
        {
            var existingData = WfDataContext.FromExpando((ExpandoObject)wfInst.Data);
            var restartData = new WfDataContext(
                data.CorpId,
                data.UserId,
                data.AccessToken,
                data.AppId,
                data.FormId,
                data.DataId,
                data.WfStarter,
                data.DfCascade,
                data.EventIds)
            {
                Round = existingData.Round
            };

            wfInst.Data = restartData.ToExpando();
            wfInst.Status = WorkflowStatus.Runnable;
            wfInst.NextExecution = 0;
            wfInst.CompleteTime = null;

            await _store.PersistWorkflow(wfInst);
            return WaitForComplete(wfInst.Id);
        }


        [HttpPost, Route("Definition/Delete")]
        public IActionResult DeleteDef(DeleteRequest request)
        {
            List<string>? defIds = null;
            if (!string.IsNullOrEmpty(request.AppId))
                defIds = _defservice.Query(x => x.AppId == request.AppId && !x.DeleteFlag).Select(x => x.Id).ToList();
            else
                defIds = _defservice.Query(x => x.FlowType == FlowType.Workflow && request.FormIds!.Contains(x.SourceId!) && !x.DeleteFlag).Select(x => x.Id).ToList();

            if (defIds?.Count > 0)
            {
                var wfInsts = _store.GetWorkflowInstancesByDefId(defIds, WorkflowStatus.Runnable).Select(x => x.Id);
                wfInsts.ForEach(x => _wfHost.TerminateWorkflow(x));
            }

            if (request.DeleteDef.HasValue && request.DeleteDef.Value)
            {
                if (!string.IsNullOrEmpty(request.AppId))
                    _defservice.Delete(new DynamicFilter() { Rel = "and", Items = [new DynamicFilter { Field = "deleteFlag", Op = FilterOp.Eq, Value = false }, new DynamicFilter { Field = "appId", Op = FilterOp.Eq, Value = request.AppId }] });
                else if (defIds?.Count > 0)
                    _defservice.Delete(defIds);
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

            var exist = _defservice.Find(x => x.ExternalId == def.ExternalId && x.Version == def.Version).FirstOrDefault();
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
            return Ok(_defservice.Find(x => x.ExternalId == request.WfDefinitionId).ToList());
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
        public CascadeMode DfCascade { get; set; }
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
}
