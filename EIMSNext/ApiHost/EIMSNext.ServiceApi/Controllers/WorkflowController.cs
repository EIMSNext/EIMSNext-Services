﻿using Asp.Versioning;
using EIMSNext.ApiClient.Flow;
using EIMSNext.Core;
using EIMSNext.Entity;
using EIMSNext.ServiceApi.Request;
using HKH.Mef2.Integration;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace EIMSNext.ServiceApi.Controllers
{
    /// <summary>
    /// 工作流
    /// </summary>
    /// <param name="resolver"></param>
    [ApiController, ApiVersion(1.0)]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class WorkflowController(IResolver resolver) : MefControllerBase<FormData, FormData>(resolver)
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost, Route("Start")]
        public async Task<IActionResult> StartAsync(WfStartRequest request)
        {
            var data = ApiService.Get(request.DataId);
            if (data != null)
            {
                var startReq = new StartRequest { DataId = data.Id, WfDefinitionId = data.FormId };
                if (data.ApprovalLogs.Count > 0)
                {
                    startReq.Version = data.ApprovalLogs.Last().WfVersion;
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
        [HttpPost, Route("Approve")]
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
    }
}
