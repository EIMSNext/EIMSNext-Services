using Asp.Versioning;

using EIMSNext.ApiHost.Controllers;
using EIMSNext.ApiHost.Extensions;
using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using System.Text.Json;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Entities;
using EIMSNext.Flow.Core.Interfaces;
using EIMSNext.Flow.Persistence;
using EIMSNext.Service.Contracts;

using HKH.Mef2.Integration;

using Microsoft.AspNetCore.Mvc;


using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Host.Controllers
{
    [ApiVersion(1.0)]
    public class EventFlowController : MefControllerBase
    {
        private readonly IWorkflowHost _wfHost;
        private readonly IWorkflowLoader _workflowLoader;
        private readonly ILogger<EventFlowController> _logger;
        private readonly IWfDefinitionService _defservice;
        private readonly IFormDataService _formDataservice;
        private readonly IEventFlowRunner _eventFlowRunner;
        private readonly IWorkflowRegistry _workflowRegistry;
        private readonly IWorkflowExecutor _workflowExecutor;
        private readonly IWorkflowPersistenceProvider _workflowPersistence;

        public EventFlowController(IResolver resolver) : base(resolver)
        {
            _wfHost = resolver.Resolve<IWorkflowHost>();
            _workflowLoader = resolver.Resolve<IWorkflowLoader>();
            _defservice = resolver.Resolve<IWfDefinitionService>();
            _logger = resolver.GetLogger<EventFlowController>();
            _formDataservice = resolver.Resolve<IFormDataService>();
            _eventFlowRunner = resolver.Resolve<IEventFlowRunner>();
            _workflowRegistry = resolver.Resolve<IWorkflowRegistry>();
            _workflowExecutor = resolver.Resolve<IWorkflowExecutor>();
            _workflowPersistence = (IWorkflowPersistenceProvider)resolver.Resolve<IPersistenceProvider>();
        }

        [HttpPost, Route("Load")]
        public IActionResult Load(EfLoadRequest request)
        {
            var def = _defservice.Find(x => x.ExternalId == request.EfDefinitionId && x.Version == request.Version).FirstOrDefault();
            if (def == null || def.FlowType != FlowType.EventFlow || def.DeleteFlag)
                return BadRequest($"数据流程定义({request.EfDefinitionId}:{request.Version})不存在");

            _workflowLoader.LoadDefinition(def);

            return ApiResult.Success(new { id = request.EfDefinitionId }).ToActionResult();
        }

        [HttpPost, Route("Run")]
        public async Task<IActionResult> RunAsync(EfRunRequest request)
        {
            if (!IsSystemIdentity && (request.EventType.HasFlag(EventType.Approving)
                || !string.IsNullOrWhiteSpace(request.NodeAction)))
            {
                return BadRequest("外部 EventFlow 不允许触发工作流节点流转");
            }

            if (IsSystemIdentity && string.IsNullOrWhiteSpace(request.EventFlowId)
                && !request.MatchAllEventFlows)
            {
                return BadRequest("系统触发 EventFlow 时必须明确指定 EventFlowId 或 MatchAllEventFlows");
            }

            if (!string.IsNullOrWhiteSpace(request.RunLogId))
            {
                return await RetryExistingWorkflowAsync(request);
            }

            FormData? formData = null;
            Wf_Definition? eventFlow = null;

            if (!string.IsNullOrEmpty(request.EventFlowId))
            {
                eventFlow = _defservice.Get(request.EventFlowId);
                if (eventFlow == null || eventFlow.FlowType != FlowType.EventFlow || eventFlow.DeleteFlag || eventFlow.Disabled)
                {
                    return BadRequest("数据流定义不存在或已禁用");
                }

                if (!IsSystemIdentity && !BelongsToCurrentCorp(eventFlow.CorpId))
                {
                    return NotFound("数据流定义不存在");
                }
            }

            if (!string.IsNullOrEmpty(request.DataId))
            {
                // 删除事件由业务侧在软删除之后触发，此时全局过滤已隐藏该行；必须显式忽略软删过滤，
                // 否则 Removed 事件流永远取不到数据、无法真正执行。
                formData = request.EventType.HasFlag(EventType.Removed)
                    ? _formDataservice.GetIncludingDeleted(request.DataId)
                    : _formDataservice.Get(request.DataId);

                if (formData == null)
                {
                    // 删除事件取不到数据时不能当成错误抛回业务侧，否则删除接口整体 500。
                    if (request.EventType.HasFlag(EventType.Removed))
                    {
                        return ApiResult.Success(new { Id = string.Empty, Error = string.Empty }).ToActionResult();
                    }

                    return NotFound("数据不存在");
                }

                if (!IsSystemIdentity && !BelongsToCurrentCorp(formData.CorpId))
                {
                    return NotFound("数据不存在");
                }
            }
            else if (eventFlow != null)
            {
                formData = new FormData
                {
                    CorpId = eventFlow.CorpId,
                    AppId = eventFlow.AppId,
                    FormId = eventFlow.SourceId ?? string.Empty,
                    Data = new Dictionary<string, object?>()
                };
            }
            else
            {
                return BadRequest("数据Id不能为空");
            }

            var runParamter = new EfRunParameter(
                    IdentityContext.CurrentUserID,
                    IdentityContext.AccessToken,
                    formData,
                    request.EventSource,
                    request.EventType,
                    request.WfNodeId,
                    ResolveStarter(request))
                .WithExecutionId(request.ExecutionId)
                .WithEventFlowId(request.EventFlowId)
                .WithNodeAction(request.NodeAction)
                .WithChangeFields(request.ChangeFields)
                .WithChain(request.ChainJson?.DeserializeFromJson<EfChainContext>());

            var efExecResult = await _eventFlowRunner.RunAsync(runParamter);

            if (!efExecResult.Success)
            {
                return ApiResult.Success(new { Id = efExecResult.EfInstance?.Id, Error = efExecResult.Error }).ToActionResult();
            }

            return ApiResult.Success(new { Id = efExecResult.EfInstance?.Id ?? "", Error = "" }).ToActionResult();
        }

        private async Task<IActionResult> RetryExistingWorkflowAsync(EfRunRequest request)
        {
            if (!IsSystemIdentity)
            {
                return Forbid();
            }

            var runLogRepository = Resolver.GetRepository<Ef_RunLog>();
            var runLog = runLogRepository.Get(request.RunLogId!);
            if (runLog == null || runLog.DeleteFlag || !string.Equals(runLog.CorpId, IdentityContext.CurrentCorpId, StringComparison.OrdinalIgnoreCase))
            {
                return NotFound("运行日志不存在");
            }

            try
            {
                if (string.IsNullOrWhiteSpace(request.RetryAttemptId)
                    || !string.Equals(runLog.RetryAttemptId, request.RetryAttemptId, StringComparison.OrdinalIgnoreCase))
                {
                    return Conflict("该重试已被其他尝试接管");
                }

                if (string.IsNullOrWhiteSpace(runLog.WfInstanceId))
                {
                    return BadRequest("该运行没有可恢复的工作流实例");
                }

                if (!string.Equals(runLog.EventFlowId, request.EventFlowId, StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest("重试的 EF 定义与原运行不一致");
                }

                var instance = await _workflowPersistence.GetWorkflowInstance(runLog.WfInstanceId);
                var definition = _workflowRegistry.GetDefinition(instance.WorkflowDefinitionId, instance.Version);
                if (definition == null)
                {
                    return BadRequest("原 EF 工作流定义不可用，无法验证重试节点");
                }

                var step = definition.Steps.FirstOrDefault(x => string.Equals(x.ExternalId, request.RetryNodeId, StringComparison.OrdinalIgnoreCase));
                if (step == null)
                {
                    return BadRequest("重试节点不属于原 EF 工作流");
                }

                var pointer = instance.ExecutionPointers.FirstOrDefault(x => x.StepId == step.Id && x.Status == PointerStatus.Failed);
                if (pointer == null)
                {
                    const string noFailedPointerReason = "重试时未找到失败节点指针，按原 EF 已完成处理。";
                    var completedAt = DateTime.UtcNow.ToTimeStampMs();
                    // 仅更新结果字段，重试占用状态由 finally 统一释放。
                    await runLogRepository.UpdateManyAsync(
                        x => x.Id == request.RunLogId && x.RetryAttemptId == request.RetryAttemptId,
                        setters => setters
                            .SetProperty(x => x.Success, true)
                            .SetProperty(x => x.ErrMsg, noFailedPointerReason)
                            .SetProperty(x => x.EndTime, completedAt)
                            .SetProperty(x => x.UpdateTime, completedAt));

                    var nodeLogRepository = Resolver.GetRepository<Ef_RunLogNode>();
                    var failedNodeLog = nodeLogRepository.Find(x =>
                            x.RunLogId == request.RunLogId
                            && x.CorpId == runLog.CorpId
                            && x.NodeId == request.RetryNodeId
                            && !x.Success)
                        .FirstOrDefault();
                    if (failedNodeLog != null)
                    {
                        await nodeLogRepository.UpdateAsync(failedNodeLog.Id, setters => setters
                            .SetProperty(x => x.Success, true)
                            .SetProperty(x => x.ErrMsg, noFailedPointerReason)
                            .SetProperty(x => x.FailureReason, noFailedPointerReason)
                            .SetProperty(x => x.Summary, noFailedPointerReason)
                            .SetProperty(x => x.EndTime, completedAt)
                            .SetProperty(x => x.ExecTime, completedAt));
                    }

                    return ApiResult.Success(new { Id = instance.Id, Error = string.Empty }).ToActionResult();
                }

                var nodeExecutionRepository = Resolver.GetRepository<EventFlowNodeExecution>();
                await nodeExecutionRepository.UpdateManyAsync(
                    x => x.RunLogId == request.RunLogId
                        && x.NodeId == request.RetryNodeId
                        && x.Status == EventFlowNodeExecutionStatus.Processing,
                    setters => setters
                        .SetProperty(x => x.Status, EventFlowNodeExecutionStatus.Failed)
                        .SetProperty(x => x.ProcessingOwner, string.Empty)
                        .SetProperty(x => x.LeaseUntil, 0));

                pointer.Status = PointerStatus.Pending;
                pointer.Active = true;
                pointer.StartTime = null;
                pointer.EndTime = null;
                pointer.EventData = null;
                pointer.EventPublished = false;
                pointer.RetryCount++;
                instance.Status = WorkflowStatus.Runnable;
                instance.CompleteTime = null;
                instance.NextExecution = DateTime.UtcNow.Ticks;

                try
                {
                    var executionResult = await _workflowExecutor.Execute(instance, CancellationToken.None);
                    if (instance.Status == WorkflowStatus.Complete)
                    {
                        instance.CompleteTime ??= DateTime.UtcNow;
                        instance.NextExecution = null;
                    }
                    await _workflowPersistence.PersistWorkflow(instance, executionResult.Subscriptions ?? []);
                    if (executionResult.Errors.Count > 0)
                    {
                        await _workflowPersistence.PersistErrors(executionResult.Errors, CancellationToken.None);
                    }

                    var error = executionResult.Errors.Count == 0
                        ? string.Empty
                        : string.Join("; ", executionResult.Errors.Select(x => x.Message));
                    var completedAt = DateTime.UtcNow.ToTimeStampMs();
                    // 仅更新结果字段，重试占用状态由 finally 统一释放。
                    await runLogRepository.UpdateManyAsync(
                        x => x.Id == request.RunLogId && x.RetryAttemptId == request.RetryAttemptId,
                        setters => setters
                            .SetProperty(x => x.Success, instance.Status == WorkflowStatus.Complete && executionResult.Errors.Count == 0)
                            .SetProperty(x => x.ErrMsg, error)
                            .SetProperty(x => x.EndTime, completedAt)
                            .SetProperty(x => x.UpdateTime, completedAt));
                    return ApiResult.Success(new { Id = instance.Id, Error = error }).ToActionResult();
                }
                catch (Exception ex)
                {
                    var failedAt = DateTime.UtcNow.ToTimeStampMs();
                    await runLogRepository.UpdateManyAsync(
                        x => x.Id == request.RunLogId && x.RetryAttemptId == request.RetryAttemptId,
                        setters => setters
                            .SetProperty(x => x.Success, false)
                            .SetProperty(x => x.ErrMsg, ex.Message)
                            .SetProperty(x => x.EndTime, failedAt)
                            .SetProperty(x => x.UpdateTime, failedAt));
                    throw;
                }
            }
            finally
            {
                // 重试占用释放的唯一收口：提前返回/执行异常等路径均经此处，避免租约被占满 30 分钟。
                if (runLog.RetryInProgress)
                {
                    try
                    {
                        var now = DateTime.UtcNow.ToTimeStampMs();
                        await runLogRepository.UpdateManyAsync(
                            x => x.Id == request.RunLogId
                                && x.RetryAttemptId == request.RetryAttemptId,
                            setters => setters
                                .SetProperty(x => x.RetryInProgress, false)
                                .SetProperty(x => x.RetryLeaseUntil, (long?)null)
                                .SetProperty(x => x.RetryAttemptId, string.Empty)
                                .SetProperty(x => x.UpdateTime, now));
                    }
                    catch (Exception cleanupException)
                    {
                        _logger.LogError(cleanupException, "清理 EF 重试状态失败。RunLogId={RunLogId}", request.RunLogId);
                    }
                }
            }
        }

        private bool IsSystemIdentity => IdentityContext.IdentityType == ApiService.IdentityType.System;

        private bool BelongsToCurrentCorp(string? corpId)
        {
            return !string.IsNullOrWhiteSpace(IdentityContext.CurrentCorpId) &&
                   string.Equals(corpId, IdentityContext.CurrentCorpId, StringComparison.OrdinalIgnoreCase);
        }

        private Operator? ResolveStarter(EfRunRequest request)
        {
            if (IdentityContext.IdentityType == ApiService.IdentityType.System)
            {
                if (!string.IsNullOrWhiteSpace(request.StarterId))
                {
                    return new Operator(
                        request.StarterId,
                        request.StarterCode ?? string.Empty,
                        request.StarterName ?? request.StarterCode ?? "System");
                }

                var objectName = string.IsNullOrWhiteSpace(request.EventFlowId) ? "ef_unknown" : $"ef_{request.EventFlowId}";
                return new Operator("system", objectName, "System");
            }

            return IdentityContext.CurrentEmployee?.ToOperator();
        }

#if DEBUG
        //有些方法将写在API中，此处为调试用
        [HttpPost, Route("Definition/Create")]
        public IActionResult Create(EfCreateRequest request)
        {
            var def = new Wf_Definition()
            {
                Version = request.Version,
                Content = request.Content,
                ExternalId = request.EfDefinitionId,
                FlowType = FlowType.EventFlow,
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
        public IActionResult GetDefinition([FromQuery] EfCreateRequest request)
        {
            return Ok(_defservice.Find(x => x.ExternalId == request.EfDefinitionId).ToList());
        }
#endif
    }
#if DEBUG
    public class EfCreateRequest()
    {
        public string EfDefinitionId { get; set; } = string.Empty;
        public int Version { get; set; }
        public string Content { get; set; } = string.Empty;
    }
#endif
    public class EfLoadRequest()
    {
        public string EfDefinitionId { get; set; } = string.Empty;
        public int Version { get; set; }
    }
    public class EfRunRequest
    {
        public string ExecutionId { get; set; } = string.Empty;
        public string? RunLogId { get; set; }
        public string? RetryNodeId { get; set; }
        public string? RetryAttemptId { get; set; }
        public string EventFlowId { get; set; } = string.Empty;
        /// <summary>EventFlowId 为空时，是否明确允许匹配全部候选 EF。</summary>
        public bool MatchAllEventFlows { get; set; }
        public string DataId { get; set; } = string.Empty;
        public EventSourceType EventSource { get; set; }
        public EventType EventType { get; set; }
        public string WfNodeId { get; set; } = string.Empty;
        public string? NodeAction { get; set; }
        public string? StarterId { get; set; }
        public string? StarterCode { get; set; }
        public string? StarterName { get; set; }
        /// <summary>EventFlow 链上下文 JSON（EfChainContext）：EF 写数据触发下游时透传，供链尾解析出站门禁；入口触发为空。</summary>
        public string? ChainJson { get; set; }
        public List<string>? ChangeFields { get; set; }
    }
}

