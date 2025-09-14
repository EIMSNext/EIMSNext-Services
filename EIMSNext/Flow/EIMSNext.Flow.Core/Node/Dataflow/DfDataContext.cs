using System.Text.Json;
using EIMSNext.Common.Extension;
using EIMSNext.Core.Entity;
using EIMSNext.Entity;

namespace EIMSNext.Flow.Core
{
    public class DfDataContext
    {
        public string CorpId { get; set; } = string.Empty;
        public string AppId { get; set; } = string.Empty;
        public string? FormId { get; set; }
        public string? DataId { get; set; }
        public Operator? WfStarter { get; set; }
        public string? ErrMsg { get; set; }

        //public FormData? TriggerData { get; set; }

        public Dictionary<string, FormDef> FormDefs { get; set; } = new Dictionary<string, FormDef>();
        public Dictionary<string, DfNodeData> NodeDatas { get; set; } = new Dictionary<string, DfNodeData>();

        public CascadeMode DfCascade { get; set; }
        public string? EventIds { get; set; }

        public bool MatchedResult { get; set; }
        public bool MatchParallel { get; set; }
    }
    public sealed class DfNodeData
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

    }
    public enum DataState
    {
        Unchanged = 0,
        Inserted = 1,
        Modified = 2,
        Removed = 3
    }
}
