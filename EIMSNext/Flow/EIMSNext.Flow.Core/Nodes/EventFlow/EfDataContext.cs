using System.Text.Json;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Entities;

namespace EIMSNext.Flow.Core
{
    public class EfDataContext
    {
        public string CorpId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public string AppId { get; set; } = string.Empty;
        public string EventFlowId { get; set; } = string.Empty;
        public string RunLogId { get; set; } = string.Empty;
        public string ExecutionId { get; set; } = string.Empty;
        public string WfNodeId { get; set; } = string.Empty;
        public string NodeAction { get; set; } = string.Empty;
        public string? FormId { get; set; }
        public string? DataId { get; set; }
        public Operator? WfStarter { get; set; }
        public string? ErrMsg { get; set; }

        public FormData? TriggerData { get; set; }

        public Dictionary<string, FormDef> FormDefs { get; set; } = new Dictionary<string, FormDef>();
        public Dictionary<string, EfNodeData> NodeDatas { get; set; } = new Dictionary<string, EfNodeData>();

        /// <summary>
        /// 打印节点待上传产物（key=打印节点Id）。仅存于 run 进程内存：下游首次消费 printFile 时才真正上传，
        /// 未被消费则随 run 结束丢弃，不产生附件。不参与 WorkflowCore 持久化。
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public Dictionary<string, EfPendingPrintFile> PendingPrints { get; } = new();

        /// <summary>EventFlow 链上下文：null = 新链；随本对象持久化并向下游传播。</summary>
        public EfChainContext? Chain { get; set; }

        public bool MatchedResult { get; set; }
        public bool MatchParallel { get; set; }
    }
    public sealed class EfNodeData
    {
        private List<ActionFormData>? _formData;
        public string NodeId { get; set; } = string.Empty;
        public bool SingleResult { get; set; }
        public string? FormId { get; set; }       
        public object? NodeExecResult { get; set; }
        public string? FormDataStr { get; set; }

        //由于ExpandObject只对System.Text.Json做了重写，
        //WorkflowCore内部使用Newtonsoft操作有时会有问题
        //并且DeepSeek建议大数据放到缓存中，将来对象中可能只是一个缓存Key
        //所以此处做对象转换        
        [Newtonsoft.Json.JsonIgnore]
        public List<ActionFormData> ActionDatas
        {
            get
            {
                if (_formData == null)
                {
                    if (!string.IsNullOrEmpty(FormDataStr))
                        _formData = FormDataStr.DeserializeFromJson<List<ActionFormData>>();
                    _formData = _formData ?? new List<ActionFormData>();
                }

                return _formData;
            }
            set
            {
                //数据较小时存内容，如果将来数据将大，则应存到Redis缓存或数据表
                _formData = value;
                FormDataStr = _formData.SerializeToJson();
            }
        }
    }
    public sealed class ActionFormData
    {
        public DataState State { get; set; }
        public required FormData FormData { get; set; }
        public bool Persisted { get; set; }

    }

    /// <summary>打印节点渲染出、等待首次消费时上传的文件。</summary>
    public sealed class EfPendingPrintFile
    {
        /// <summary>printFile 输出中的待上传标记键，值为打印节点 Id。</summary>
        public const string MarkerKey = "__pendingPrint";

        /// <summary>重渲染要素：run 跨进程恢复后产物丢失时按此就地重渲染（随 marker 持久化）。</summary>
        public const string PrintDefIdKey = "printDefId";
        public const string FormIdKey = "formId";
        public const string SourceNodeIdKey = "sourceNodeId";

        public required string FileName { get; init; }
        public required byte[] Content { get; init; }
        public long FileSize { get; init; }

        /// <summary>首次消费上传后的附件信息缓存（同一 run 内多个消费点共享一次上传）。</summary>
        public Dictionary<string, object?>? Attachment { get; set; }
    }
    public enum DataState
    {
        Unchanged = 0,
        Inserted = 1,
        Modified = 2,
        Removed = 3
    }
}
