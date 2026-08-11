using Asp.Versioning;
using EIMSNext.ApiClient.Flow;
using EIMSNext.ApiHost.Extensions;
using EIMSNext.ApiService;
using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Host.Authorization;
using EIMSNext.Service.Host.Models;
using EIMSNext.Service.Host.Requests;
using HKH.Mef2.Integration;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using EIMSNext.Common.Extensions;

namespace EIMSNext.Service.Host.Controllers
{
    /// <summary>
    /// 工作流
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
    public class WorkflowController(IResolver resolver) : MefControllerBase<FormDataApiService, FormData, FormData>(resolver)
    {
        [HttpGet("ManageTasks")]
        [IdentityType(IdentityType.CorpAdmin)]
        public IActionResult ManageTasks([FromQuery] FlowManageQueryRequest request)
        {
            var pageNum = request.PageNum <= 0 ? 1 : request.PageNum;
            var pageSize = request.PageSize <= 0 ? 20 : Math.Min(request.PageSize, 100);
            var keyword = request.Keyword?.Trim() ?? string.Empty;

            var taskService = Resolver.GetService<Wf_Task>();
            var employeeService = Resolver.GetService<Employee>();
            var departmentService = Resolver.GetService<Department>();
            var employeeDepartmentRepo = Resolver.GetRepository<EmployeeDepartment>();
            var formDefRepo = Resolver.GetRepository<FormDef>();

            var query = taskService.Query(x => x.CorpId == IdentityContext.CurrentCorpId);
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(x => x.DataId.Contains(keyword));
            }

            var total = query.LongCount();
            var pagedTasks = query
                .OrderByDescending(x => x.ApproveNodeStartTime)
                .Skip((pageNum - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var employeeIds = pagedTasks.Select(x => x.EmployeeId).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            var employeeMap = employeeService.Query(x => employeeIds.Contains(x.Id)).ToList().ToDictionary(x => x.Id, x => x);

            var employeeDepartments = employeeDepartmentRepo.Queryable
                .Where(x => employeeIds.Contains(x.EmployeeId))
                .OrderBy(x => x.SortValue)
                .ToList();
            var deptIds = employeeDepartments.Select(x => x.DepartmentId).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            var deptMap = departmentService.Query(x => deptIds.Contains(x.Id)).ToList().ToDictionary(x => x.Id, x => x.Name);
            var employeeDepartmentMap = employeeDepartments
                .GroupBy(x => x.EmployeeId)
                .ToDictionary(x => x.Key, x => x.Select(y => y.DepartmentId).ToList());

            var items = pagedTasks.Select(task =>
            {
                employeeMap.TryGetValue(task.EmployeeId, out var employee);
                var departmentName = employeeDepartmentMap.TryGetValue(task.EmployeeId, out var employeeDeptIds)
                    ? string.Join(", ", employeeDeptIds.Where(deptMap.ContainsKey).Select(x => deptMap[x]))
                    : string.Empty;

                return new FlowManageTaskItem
                {
                    TaskId = task.Id,
                    WfInstanceId = task.WfInstanceId,
                    DataId = task.DataId,
                    FormName = formDefRepo.Get(task.FormId)?.Name ?? string.Empty,
                    Starter = task.Starter,
                    CurrentApproverName = employee?.EmpName ?? string.Empty,
                    DepartmentName = departmentName,
                    ApproveNodeId = task.ApproveNodeId,
                    ApproveNodeName = task.ApproveNodeName,
                    ApproveNodeStartTime = task.ApproveNodeStartTime,
                };
            }).ToList();

            return ApiResult.Success(new FlowManageTaskQueryResult
            {
                Items = items,
                Total = total,
            }).ToActionResult();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("Start")]
        public async Task<IActionResult> StartAsync(WfStartRequest request)
        {
            var data = ApiService.Get(request.DataId);
            var dataError = EnsureWorkflowData(data, "发起流程失败：数据不存在");
            if (dataError != null) return dataError;
            if (data != null)
            {
                var taskLogService = this.Resolver.GetService<Wf_TaskLog>();
                var wfDefinitionService = this.Resolver.Resolve<IWfDefinitionService>();
                var taskLog = taskLogService.Query(x => x.DataId == request.DataId).FirstOrDefault();
                var currentDef = wfDefinitionService.Find(data.FormId);

                if (currentDef == null && taskLog == null)
                {
                    return Error(-1, "发起流程失败：未找到已启用流程版本");
                }

                var startReq = new StartRequest
                {
                    DataId = data.Id,
                    WfDefinitionId = data.FormId,
                    Version = currentDef?.Version ?? 0,
                };

                //此处有可能是重新发起流程，应使用之前的流程版本
                if (taskLog != null)
                {
                    startReq.Version = taskLog.WfVersion;
                }

                var flowClient = Resolver.Resolve<FlowApiClient>();
                var resp = await flowClient.Start(startReq, IdentityContext.AccessToken);
                if (resp != null && string.IsNullOrEmpty(resp.Error))
                    return Ok(resp);
                else
                    return Error(-1, $"发起流程失败：{resp?.Error}");
            }
            else
            {
                return Error(-1, "发起流程失败：数据不存在");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("Approve")]
        public async Task<IActionResult> Approve(WfApproveRequest request)
        {
            var data = ApiService.Get(request.DataId);
            var dataError = EnsureWorkflowData(data, "审批流程失败：数据不存在");
            if (dataError != null) return dataError;
            if (data != null)
            {
                var taskService = Resolver.GetService<Wf_Task>();
                var currentEmployeeId = IdentityContext.CurrentEmployee?.Id;
                var task = taskService.Query(x => x.DataId == request.DataId &&
                    x.EmployeeId == currentEmployeeId &&
                    x.CorpId == IdentityContext.CurrentCorpId).FirstOrDefault();
                if (task != null)
                {
                    var approveReq = new ApproveRequest
                    {
                        WfInstanceId = task.WfInstanceId,
                        DataId = task.DataId,
                        WfNodeId = task.ApproveNodeId,
                        Action = request.Action,
                        Comment = request.Comment,
                        Signature = request.Signature,
                    };

                    var flowClient = Resolver.Resolve<FlowApiClient>();
                    var resp = await flowClient.Approve(approveReq, IdentityContext.AccessToken);
                    if (resp != null && string.IsNullOrEmpty(resp.Error))
                        return Ok(resp);
                    else
                        return Error(-1, $"审批流程失败：{resp?.Error}");
                }
                else
                {
                    return Error(StatusCodes.Status403Forbidden, "审批流程失败：没有审批权限");
                }
            }
            else
            {
                return Error(-1, "审批流程失败：数据不存在");
            }
        }

        [HttpPost("Submit")]
        public async Task<IActionResult> Submit(WfApproveRequest request)
        {
            request.Action = EIMSNext.ApiClient.Flow.ApproveAction.Approve;
            return await Approve(request);
        }

        [HttpPost("Reject")]
        public async Task<IActionResult> Reject(WfApproveRequest request)
        {
            request.Action = EIMSNext.ApiClient.Flow.ApproveAction.Reject;
            return await Approve(request);
        }

        [HttpPost("Return")]
        public async Task<IActionResult> Return(WfReturnRequest request)
        {
            var data = ApiService.Get(request.DataId);
            var dataError = EnsureWorkflowData(data, "回退流程失败：数据不存在");
            if (dataError != null) return dataError;
            if (data == null)
            {
                return Error(-1, "回退流程失败：数据不存在");
            }

            var flowClient = Resolver.Resolve<FlowApiClient>();
            var resp = await flowClient.Return(new ReturnRequest
            {
                WfInstanceId = request.WfInstanceId,
                DataId = data.Id,
                WfNodeId = request.WfNodeId,
                TargetNodeId = request.TargetNodeId,
                Comment = request.Comment,
            }, IdentityContext.AccessToken);

            if (resp != null && string.IsNullOrEmpty(resp.Error))
            {
                return Ok(resp);
            }

            return Error(-1, $"回退流程失败：{resp?.Error}");
        }

        [HttpPost("AddSign")]
        public async Task<IActionResult> AddSign(WfAddSignRequest request)
        {
            var data = ApiService.Get(request.DataId);
            var dataError = EnsureWorkflowData(data, "加签流程失败：数据不存在");
            if (dataError != null) return dataError;
            if (data == null)
            {
                return Error(-1, "加签流程失败：数据不存在");
            }

            var flowClient = Resolver.Resolve<FlowApiClient>();
            var resp = await flowClient.AddSign(new AddSignRequest
            {
                WfInstanceId = request.WfInstanceId,
                DataId = data.Id,
                WfNodeId = request.WfNodeId,
                TargetEmployeeId = request.TargetEmployeeId,
                Comment = request.Comment,
            }, IdentityContext.AccessToken);

            if (resp != null && string.IsNullOrEmpty(resp.Error))
            {
                return Ok(resp);
            }

            return Error(-1, $"加签流程失败：{resp?.Error}");
        }

        [HttpPost("Transfer")]
        public async Task<IActionResult> Transfer(WfTransferRequest request)
        {
            var data = ApiService.Get(request.DataId);
            var dataError = EnsureWorkflowData(data, "转交流程失败：数据不存在");
            if (dataError != null) return dataError;
            if (data == null)
            {
                return Error(-1, "转交流程失败：数据不存在");
            }

            var flowClient = Resolver.Resolve<FlowApiClient>();
            var resp = await flowClient.Transfer(new TransferRequest
            {
                WfInstanceId = request.WfInstanceId,
                DataId = data.Id,
                WfNodeId = request.WfNodeId,
                TargetEmployeeId = request.TargetEmployeeId,
                Comment = request.Comment,
            }, IdentityContext.AccessToken);

            if (resp != null && string.IsNullOrEmpty(resp.Error))
            {
                return Ok(resp);
            }

            return Error(-1, $"转交流程失败：{resp?.Error}");
        }

        [HttpPost("Withdraw")]
        public async Task<IActionResult> Withdraw(WfWithdrawRequest request)
        {
            var data = ApiService.Get(request.DataId);
            var dataError = EnsureWorkflowData(data, "撤回流程失败：数据不存在");
            if (dataError != null) return dataError;
            if (data == null)
            {
                return Error(-1, "撤回流程失败：数据不存在");
            }

            var flowClient = Resolver.Resolve<FlowApiClient>();
            var resp = await flowClient.Withdraw(new WithdrawRequest
            {
                WfInstanceId = request.WfInstanceId,
                DataId = data.Id,
                Comment = request.Comment,
            }, IdentityContext.AccessToken);

            if (resp != null && string.IsNullOrEmpty(resp.Error))
            {
                data.FlowStatus = FlowStatus.Draft;
                await ApiService.ReplaceAsync(data, DataAction.Save);
                return Ok(resp);
            }

            return Error(-1, $"撤回流程失败：{resp?.Error}");
        }

        [HttpPost("Urge")]
        public async Task<IActionResult> Urge(WfUrgeRequest request)
        {
            var data = ApiService.Get(request.DataId);
            var dataError = EnsureWorkflowData(data, "催办流程失败：数据不存在");
            if (dataError != null) return dataError;
            if (data == null)
            {
                return Error(-1, "催办流程失败：数据不存在");
            }

            var flowClient = Resolver.Resolve<FlowApiClient>();
            var resp = await flowClient.Urge(new UrgeRequest
            {
                WfInstanceId = request.WfInstanceId,
                DataId = data.Id,
            }, IdentityContext.AccessToken);

            if (resp != null && string.IsNullOrEmpty(resp.Error))
                return Ok(resp);

            return Error(-1, $"催办流程失败：{resp?.Error}");
        }

        [HttpGet("ActionStatus")]
        public async Task<IActionResult> ActionStatus([FromQuery] WfActionStatusRequest request)
        {
            var data = ApiService.Get(request.DataId);
            var dataError = EnsureWorkflowData(data, "获取流程操作状态失败：数据不存在");
            if (dataError != null) return dataError;
            if (data == null)
            {
                return Error(-1, "获取流程操作状态失败：数据不存在");
            }

            var flowClient = Resolver.Resolve<FlowApiClient>();
            var resp = await flowClient.ActionStatus(new ActionStatusRequest
            {
                WfInstanceId = request.WfInstanceId,
                DataId = data.Id,
            }, IdentityContext.AccessToken);

            if (resp != null && string.IsNullOrEmpty(resp.Error))
                return Ok(resp);

            return Error(-1, $"获取流程操作状态失败：{resp?.Error}");
        }

        [HttpGet("NodeActions")]
        public async Task<IActionResult> NodeActions([FromQuery] WfActionStatusRequest request)
        {
            var data = ApiService.Get(request.DataId);
            var dataError = EnsureWorkflowData(data, "获取流程节点操作失败：数据不存在");
            if (dataError != null) return dataError;
            if (data == null)
            {
                return Error(-1, "获取流程节点操作失败：数据不存在");
            }

            var flowClient = Resolver.Resolve<FlowApiClient>();
            var actions = await flowClient.NodeActions(new ActionStatusRequest
            {
                WfInstanceId = request.WfInstanceId,
                DataId = data.Id,
            }, IdentityContext.AccessToken);

            return Ok(actions ?? []);
        }

        [HttpGet("ReturnNodes")]
        public async Task<IActionResult> ReturnNodes([FromQuery] WfActionStatusRequest request)
        {
            var data = ApiService.Get(request.DataId);
            var dataError = EnsureWorkflowData(data, "获取回退节点失败：数据不存在");
            if (dataError != null) return dataError;
            if (data == null)
            {
                return Error(-1, "获取回退节点失败：数据不存在");
            }

            var flowClient = Resolver.Resolve<FlowApiClient>();
            var resp = await flowClient.ReturnNodes(new ActionStatusRequest
            {
                WfInstanceId = request.WfInstanceId,
                DataId = data.Id,
            }, IdentityContext.AccessToken);

            return Ok(resp ?? []);
        }

        [HttpPost("Terminate")]
        [IdentityType(IdentityType.CorpAdmin)]
        public async Task<IActionResult> Terminate(WfTerminateRequest request)
        {
            var data = ApiService.Get(request.DataId);
            var dataError = EnsureWorkflowData(data, "废弃流程失败：数据不存在");
            if (dataError != null) return dataError;
            if (data == null)
            {
                return Error(-1, "废弃流程失败：数据不存在");
            }

            var flowClient = Resolver.Resolve<FlowApiClient>();
            var resp = await flowClient.Terminate(new TerminateRequest
            {
                WfInstanceId = request.WfInstanceId,
                DataId = request.DataId,
            }, IdentityContext.AccessToken);

            if (resp != null && string.IsNullOrEmpty(resp.Error))
            {
                Resolver.GetRepository<FormData>().Update(
                    data.Id,
                    Builders<FormData>.Update
                        .Set(x => x.FlowStatus, FlowStatus.Discarded)
                        .Set(x => x.UpdateTime, DateTime.UtcNow.ToTimeStampMs()));
                return Ok(resp);
            }

            return Error(-1, $"废弃流程失败：{resp?.Error}");
        }

        [HttpPost("ChangeApprover")]
        [IdentityType(IdentityType.CorpAdmin)]
        public async Task<IActionResult> ChangeApprover(WfChangeApproverRequest request)
        {
            var dataError = EnsureWorkflowData(ApiService.Get(request.DataId), "变更审批人失败：数据不存在");
            if (dataError != null) return dataError;

            var flowClient = Resolver.Resolve<FlowApiClient>();
            var resp = await flowClient.ChangeApprover(new ChangeApproverRequest
            {
                WfInstanceId = request.WfInstanceId,
                DataId = request.DataId,
                WfNodeId = request.WfNodeId,
                TargetEmployeeId = request.TargetEmployeeId,
                Comment = request.Comment,
            }, IdentityContext.AccessToken);

            if (resp != null && string.IsNullOrEmpty(resp.Error))
            {
                return Ok(resp);
            }

            return Error(-1, $"变更审批人失败：{resp?.Error}");
        }

        private IActionResult? EnsureWorkflowData(FormData? data, string message)
        {
            if (data == null)
            {
                return NotFound(message);
            }

            if (IdentityContext.IdentityType != global::EIMSNext.ApiService.IdentityType.System &&
                (string.IsNullOrWhiteSpace(IdentityContext.CurrentCorpId) ||
                 !string.Equals(data.CorpId, IdentityContext.CurrentCorpId, StringComparison.Ordinal)))
            {
                return NotFound(message);
            }

            return null;
        }
    }
}
