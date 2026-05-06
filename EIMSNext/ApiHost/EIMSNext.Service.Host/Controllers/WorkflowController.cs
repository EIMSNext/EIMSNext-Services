using Asp.Versioning;
using EIMSNext.ApiClient.Flow;
using EIMSNext.ApiService;
using EIMSNext.Core;
using EIMSNext.Core.Entities;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Host.Requests;
using HKH.Mef2.Integration;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace EIMSNext.Service.Host.Controllers
{
    /// <summary>
    /// 工作流
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
    public class WorkflowController(IResolver resolver) : MefControllerBase<FormDataApiService, FormData, FormData>(resolver)
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("Start")]
        public async Task<IActionResult> StartAsync(WfStartRequest request)
        {
            var data = ApiService.Get(request.DataId);
            if (data != null)
            {
                var approvalLogService = this.Resolver.GetService<Wf_ApprovalLog>();
                var wfDefinitionService = this.Resolver.Resolve<IWfDefinitionService>();
                var approvalLog = approvalLogService.Query(x => x.DataId == request.DataId).FirstOrDefault();
                var currentDef = wfDefinitionService.Find(data.FormId);

                if (currentDef == null && approvalLog == null)
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
                if (approvalLog != null)
                {
                    startReq.Version = approvalLog.WfVersion;
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
            if (data != null)
            {
                var todoService = Resolver.GetService<Wf_Todo>();
                var todo = todoService.Query(x => x.DataId == request.DataId).FirstOrDefault();
                if (todo != null)
                {
                    var approveReq = new ApproveRequest
                    {
                        WfInstanceId = todo.WfInstanceId,
                        DataId = todo.DataId,
                        WfNodeId = todo.ApproveNodeId,
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
                    return Error(-1, "审批流程失败：没有审批权限");
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

        [HttpGet("ReturnTargets")]
        public async Task<IActionResult> ReturnTargets([FromQuery] WfActionStatusRequest request)
        {
            var data = ApiService.Get(request.DataId);
            if (data == null)
            {
                return Error(-1, "获取回退节点失败：数据不存在");
            }

            var flowClient = Resolver.Resolve<FlowApiClient>();
            var resp = await flowClient.ReturnTargets(new ActionStatusRequest
            {
                WfInstanceId = request.WfInstanceId,
                DataId = data.Id,
            }, IdentityContext.AccessToken);

            return Ok(resp ?? []);
        }
    }
}
