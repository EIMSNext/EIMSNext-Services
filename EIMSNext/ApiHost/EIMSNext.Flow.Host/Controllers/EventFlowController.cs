using Asp.Versioning;

using EIMSNext.ApiHost.Controllers;
using EIMSNext.ApiHost.Extensions;
using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Service.Entities;
using EIMSNext.Flow.Core.Interfaces;
using EIMSNext.Service.Contracts;

using HKH.Mef2.Integration;

using Microsoft.AspNetCore.Mvc;

using MongoDB.Driver;

using WorkflowCore.Interface;

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

        public EventFlowController(IResolver resolver) : base(resolver)
        {
            _wfHost = resolver.Resolve<IWorkflowHost>();
            _workflowLoader = resolver.Resolve<IWorkflowLoader>();
            _defservice = resolver.Resolve<IWfDefinitionService>();
            _logger = resolver.GetLogger<EventFlowController>();
            _formDataservice = resolver.Resolve<IFormDataService>();
            _eventFlowRunner = resolver.Resolve<IEventFlowRunner>();
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
            if (request.EfCascade == CascadeMode.Never)
                return ApiResult.Success(new { Id = "", Error = "" }).ToActionResult();

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
                formData = _formDataservice.Get(request.DataId);
                if (formData == null)
                {
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
                    Data = new System.Dynamic.ExpandoObject()
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
                    ResolveStarter(request),
                    request.EfCascade,
                    request.EventIds)
                .WithEventFlowId(request.EventFlowId)
                .WithNodeAction(request.NodeAction)
                .WithChangeFields(request.ChangeFields);

            var efExecResult = await _eventFlowRunner.RunAsync(runParamter);

            if (!efExecResult.Success)
            {
                return ApiResult.Success(new { Id = efExecResult.EfInstance?.Id, Error = efExecResult.Error }).ToActionResult();
            }

            return ApiResult.Success(new { Id = efExecResult.EfInstance?.Id ?? "", Error = "" }).ToActionResult();
        }

        private bool IsSystemIdentity => IdentityContext.IdentityType == ApiService.IdentityType.System;

        private bool BelongsToCurrentCorp(string? corpId)
        {
            return !string.IsNullOrWhiteSpace(IdentityContext.CurrentCorpId) &&
                   string.Equals(corpId, IdentityContext.CurrentCorpId, StringComparison.Ordinal);
        }

        private Operator? ResolveStarter(EfRunRequest request)
        {
            if (IdentityContext.IdentityType == ApiService.IdentityType.System)
            {
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
        public string EventFlowId { get; set; } = string.Empty;
        public string DataId { get; set; } = string.Empty;
        public EventSourceType EventSource { get; set; }
        public EventType EventType { get; set; }
        public string WfNodeId { get; set; } = string.Empty;
        public string? NodeAction { get; set; }
        public CascadeMode EfCascade { get; set; }
        public string? EventIds { get; set; }
        public List<string>? ChangeFields { get; set; }
    }
}
