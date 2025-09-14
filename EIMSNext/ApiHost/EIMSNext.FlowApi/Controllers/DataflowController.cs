using Asp.Versioning;
using EIMSNext.Common;
using EIMSNext.Core;
using EIMSNext.Entity;
using EIMSNext.Flow.Core.Interface;
using EIMSNext.FlowApi.Extension;
using EIMSNext.Service.Interface;
using HKH.Mef2.Integration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using WorkflowCore.Interface;

namespace EIMSNext.FlowApi.Controllers
{
    [Authorize]
    [ApiController, ApiVersion(1.0)]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class DataflowController : MefControllerBase
    {
        private readonly IWorkflowHost _wfHost;
        private readonly IWorkflowLoader _workflowLoader;
        private readonly ILogger<DataflowController> _logger;
        private readonly IWfDefinitionService _defservice;
        private readonly IFormDataService _formDataservice;
        private readonly IDataflowRunner _dataflowRunner;

        public DataflowController(IResolver resolver) : base(resolver)
        {
            _wfHost = resolver.Resolve<IWorkflowHost>();
            _workflowLoader = resolver.Resolve<IWorkflowLoader>();
            _defservice = resolver.Resolve<IWfDefinitionService>();
            _logger = resolver.GetLogger<DataflowController>();
            _formDataservice = resolver.Resolve<IFormDataService>();
            _dataflowRunner = resolver.Resolve<IDataflowRunner>();
        }

        [HttpPost, Route("Load")]
        public IActionResult Load(DfLoadRequest request)
        {
            var def = _defservice.Find(x => x.ExternalId == request.DfDefinitionId && x.Version == request.Version).FirstOrDefault();
            if (def == null)
                return BadRequest($"数据流程定义({request.DfDefinitionId}:{request.Version})不存在");

            _workflowLoader.LoadDefinition(def);

            return ApiResult.Success(new { id = request.DfDefinitionId }).ToActionResult();
        }

        [HttpPost, Route("Run")]
        public async Task<IActionResult> RunAsync(DfRunRequest request)
        {
            if (request.DfCascade == CascadeMode.Never)
                return ApiResult.Success(new { Id = "", ErrMsg = "" }).ToActionResult();

            if (string.IsNullOrEmpty(request.DataId))
            {
                return BadRequest("数据Id不能为空");
            }

            var formData = _formDataservice.Get(request.DataId);
            if (formData != null)
            {
                var dfExecResult = await _dataflowRunner.RunAsync(formData, request.EventSource, request.EventType, "", IdentityContext.CurrentEmployee.ToOperator(), request.DfCascade, request.EventIds);

                if (!dfExecResult.Success)
                {
                    return ApiResult.Success(new { Id = dfExecResult.DfInstance?.Id, ErrMsg = dfExecResult.Error }).ToActionResult();
                }

                return ApiResult.Success(new { Id = "", ErrMsg = "" }).ToActionResult();
            }

            return NotFound("数据不存在");
        }

#if DEBUG
        //有些方法将写在API中，此处为调试用
        [HttpPost, Route("Definition/Create")]
        public IActionResult Create(DfCreateRequest request)
        {
            var def = new Wf_Definition()
            {
                Version = request.Version,
                Content = "",
                ExternalId = request.DfDefinitionId,
                FlowType = FlowType.Dataflow,
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
        public IActionResult GetDefinition([FromQuery] DfCreateRequest request)
        {
            return Ok(_defservice.Find(x => x.ExternalId == request.DfDefinitionId).ToList());
        }
#endif
    }
#if DEBUG
    public class DfCreateRequest()
    {
        public string DfDefinitionId { get; set; } = string.Empty;
        public int Version { get; set; }
        public string Content { get; set; } = string.Empty;
    }
#endif
    public class DfLoadRequest()
    {
        public string DfDefinitionId { get; set; } = string.Empty;
        public int Version { get; set; }
    }
    public class DfRunRequest
    {
        public string DataId { get; set; } = string.Empty;
        public EventSourceType EventSource { get; set; }
        public EventType EventType { get; set; }
        public CascadeMode DfCascade { get; set; }
        public string? EventIds { get; set; }
    }
}
