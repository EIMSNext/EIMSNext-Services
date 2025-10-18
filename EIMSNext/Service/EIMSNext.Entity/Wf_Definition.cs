using EIMSNext.Common;
using EIMSNext.Core.Entity;
using EIMSNext.Core.Query;

using MongoDB.Bson.Serialization.Attributes;

namespace EIMSNext.Entity
{
    /// <summary>
    /// 
    /// </summary>
    public class Wf_Definition : CorpEntityBase
    {
        /// <summary>
        /// 
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public FlowType FlowType { get; set; } = FlowType.Workflow;
        /// <summary>
        /// 
        /// </summary>
        public string ExternalId { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public string Description { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public int Version { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public bool IsCurrent { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string Content { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public WfMetadata Metadata { get; set; } = new WfMetadata();

        //Dataflow
        /// <summary>
        /// 
        /// </summary>
        public EventSourceType EventSource { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string? SourceId { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public EventSetting? EventSetting { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    public class WfMetadata
    {
        /// <summary>
        /// 
        /// </summary>
        public string Id { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public int Version { get; set; }

        //public string DataType { get; set; }

        //执行失败后，任务挂起
        //public WorkflowErrorHandling DefaultErrorBehavior { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int DefaultErrorBehavior { get; set; } = 1;
        /// <summary>
        /// 
        /// </summary>
        public TimeSpan? DefaultErrorRetryInterval { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public List<WfStep> Steps { get; set; } = new List<WfStep>();
    }

    /// <summary>
    /// 
    /// </summary>
    public class WfStep
    {
        /// <summary>
        /// 
        /// </summary>
        public WfNodeType NodeType { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string StepType { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public string Id { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public string Name { get; set; } = string.Empty;

        //public string CancelCondition { get; set; }

        //public WorkflowErrorHandling? ErrorBehavior { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int? ErrorBehavior { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public TimeSpan? RetryInterval { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public List<List<WfStep>> Work { get; set; } = new List<List<WfStep>>();
        /// <summary>
        /// 
        /// </summary>
        public List<WfStep> CompensateWith { get; set; } = new List<WfStep>();
        /// <summary>
        /// 
        /// </summary>
        public bool Saga { get; set; } = false;
        /// <summary>
        /// 
        /// </summary>
        public string NextStepId { get; set; } = string.Empty;

        //public ExpandoObject Inputs { get; set; } = new ExpandoObject();

        //public Dictionary<string, string> Outputs { get; set; } = new Dictionary<string, string>();
        /// <summary>
        /// 
        /// </summary>
        public Dictionary<string, string> SelectNextStep { get; set; } = new Dictionary<string, string>();
        /// <summary>
        /// 
        /// </summary>
        public WfNodeSetting? WfNodeSetting { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public DfNodeSetting? DfNodeSetting { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    public class WfNodeSetting
    {
        /// <summary>
        /// 
        /// </summary>
        public WfNodeType NodeType { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public ApproveSetting? ApproveSetting { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public CopyToSetting? CopyToSetting { get; set; }
    }
    /// <summary>
    /// 
    /// </summary>
    public class ApproveSetting
    {
        /// <summary>
        /// 
        /// </summary>
        public WfApprovalMode? ApprovalMode { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public IList<ApprovalCandidate>? Candidates { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public bool? EnableCopyto { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public List<ApprovalCandidate>? CopytoCandidates { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    public class CopyToSetting
    {
        /// <summary>
        /// 
        /// </summary>
        public IList<ApprovalCandidate>? Candidates { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    public class DfNodeSetting
    {
        /// <summary>
        /// 
        /// </summary>
        public WfNodeType NodeType { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public bool SingleResult { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public TriggerSetting? TriggerSetting { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public InsertSetting? InsertSetting { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public UpdateSetting? UpdateSetting { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public DeleteSetting? DeleteSetting { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public QueryOneSetting? QueryOneSetting { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public QueryManySetting? QueryManySetting { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public PrintSetting? PrintSetting { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public PluginSetting? PluginSetting { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    public class TriggerSetting
    {
        /// <summary>
        /// 
        /// </summary>
        public EventType? EventType { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string? FormId { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public List<string>? ChangeFields { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string? Condition { get; set; }
        /// <summary>
        /// 节点流转时节点ID
        /// </summary>
        public string? WfNodeId { get; set; }
        /// <summary>
        /// 节点流转时节点操作，提交或退回
        /// </summary>
        public string? NodeAction { get; set; }

    }

    /// <summary>
    /// 
    /// </summary>
    public class InsertSetting
    {
        /// <summary>
        /// 
        /// </summary>
        public string FormId { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public List<FormFieldSetting> FieldSettings { get; set; } = new List<FormFieldSetting>();
    }

    /// <summary>
    /// 
    /// </summary>
    public class FormField
    {
        /// <summary>
        /// 
        /// </summary>
        public string FormId { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public string Field { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public string Type { get; set; } = FieldType.Input;
        /// <summary>
        /// 
        /// </summary>
        public bool IsSubField { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string? NodeId { get; set; } = string.Empty;
    }

    /// <summary>
    /// 
    /// </summary>
    public class FormFieldSetting
    {
        /// <summary>
        /// 
        /// </summary>
        public FormField Field { get; set; } = new FormField();
        /// <summary>
        /// 
        /// </summary>
        public FieldValueType ValueType { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string ValueExp { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public FormFieldValueSetting? ValueField { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    public class FormFieldValueSetting
    {
        /// <summary>
        /// 
        /// </summary>
        public FormField Field { get; set; } = new FormField();
        /// <summary>
        /// 
        /// </summary>
        public bool? SingleResultNode { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    public enum FieldValueType
    {
        /// <summary>
        /// 
        /// </summary>
        Custom,
        /// <summary>
        /// 
        /// </summary>
        Field,
        /// <summary>
        /// 
        /// </summary>
        Empty
    }

    /// <summary>
    /// 
    /// </summary>
    public class UpdateSetting
    {
        /// <summary>
        /// 
        /// </summary>
        public UpdateMode UpdateMode { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string? NodeId { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string FormId { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public List<FormFieldSetting> FieldSettings { get; set; } = new List<FormFieldSetting>();
        /// <summary>
        /// 更新时，两端对象的匹配条件，用于内存中计算
        /// </summary>
        public DataMatchSetting UpdateMatch { get; set; } = new DataMatchSetting();
        /// <summary>
        /// 
        /// </summary>
        public string? DynamicFindOptions { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public bool InsertIfNoData { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public List<FormFieldSetting> InsertFieldSettings { get; set; } = new List<FormFieldSetting>();
    }

    /// <summary>
    /// 
    /// </summary>
    public class DataMatchSetting
    {
        /// <summary>
        /// 
        /// </summary>
        public FormField Field { get; set; } = new FormField();
        /// <summary>
        /// FilterRel常量值
        /// </summary>
        public string? Rel { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public List<DataMatchSetting>? Items { get; set; }

        /// <summary>
        /// FilterOp常量值
        /// </summary>
        public string? Op { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public DataMatchValueSetting? Value { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    public class DataMatchValueSetting
    {
        /// <summary>
        /// FieldValueType常量值
        /// </summary>
        public FieldValueType Type { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public object? Value { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public FormField? FieldValue { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    public class DeleteSetting
    {
        /// <summary>
        /// 
        /// </summary>
        public UpdateMode DeleteMode { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string? NodeId { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string FormId { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public string? DynamicFindOptions { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    public class QueryOneSetting
    {
        /// <summary>
        /// 
        /// </summary>
        public string FormId { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public string? DynamicFindOptions { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    public class QueryManySetting
    {
        /// <summary>
        /// 
        /// </summary>
        public string FormId { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public string? DynamicFindOptions { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    public class PrintSetting { }

    /// <summary>
    /// 
    /// </summary>
    public class PluginSetting { }

    /// <summary>
    /// 
    /// </summary>
    public enum FlowType
    {
        /// <summary>
        /// 
        /// </summary>
        Workflow,
        /// <summary>
        /// 
        /// </summary>
        Dataflow
    }

    /// <summary>
    /// 
    /// </summary>
    public enum WfNodeType
    {
        /// <summary>
        /// 
        /// </summary>
        None,
        /// <summary>
        /// 
        /// </summary>
        Start,
        /// <summary>
        /// 
        /// </summary>
        End,
        /// <summary>
        /// 
        /// </summary>
        Branch,
        /// <summary>
        /// 
        /// </summary>
        BranchItem,
        /// <summary>
        /// 
        /// </summary>
        Condition,
        /// <summary>
        /// 
        /// </summary>
        ConditionOther,
        //工作流节点
        /// <summary>
        /// 
        /// </summary>
        Approve,
        /// <summary>
        /// 
        /// </summary>
        CopyTo,
        //数据流节点
        /// <summary>
        /// 
        /// </summary>
        QueryOne,
        /// <summary>
        /// 
        /// </summary>
        QueryMany,
        /// <summary>
        /// 
        /// </summary>
        Insert,
        /// <summary>
        /// 
        /// </summary>
        Update,
        /// <summary>
        /// 
        /// </summary>
        Delete,
        /// <summary>
        /// 
        /// </summary>
        Print,
        /// <summary>
        /// 
        /// </summary>
        Branch2,
    }

    /// <summary>
    /// 
    /// </summary>
    public enum WfApprovalMode
    {
        /// <summary>
        /// 
        /// </summary>
        None,
        /// <summary>
        /// 或签
        /// </summary>
        OrSign,
        /// <summary>
        /// 会签
        /// </summary>
        CounterSign,
        /// <summary>
        /// 自动签
        /// </summary>
        AutoSign
    }

    /// <summary>
    /// 
    /// </summary>
    public class ApprovalCandidate
    {
        /// <summary>
        /// 
        /// </summary>
        public CandidateType CandidateType { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string CandidateId { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public string? CandidateName { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    public enum CandidateType
    {
        /// <summary>
        /// 
        /// </summary>
        None,
        /// <summary>
        /// 
        /// </summary>
        Department,
        /// <summary>
        /// 
        /// </summary>
        Employee,
        /// <summary>
        /// 
        /// </summary>
        Role,
        /// <summary>
        /// 
        /// </summary>
        Dynamic,
    }

    /// <summary>
    /// 
    /// </summary>
    public enum EventSourceType
    {
        /// <summary>
        /// 
        /// </summary>
        None,
        /// <summary>
        /// 
        /// </summary>
        Form,
        /// <summary>
        /// 
        /// </summary>
        Buttton
    }

    /// <summary>
    /// 
    /// </summary>
    [Flags]
    public enum EventType
    {
        /// <summary>
        /// 
        /// </summary>
        None = 0,
        /// <summary>
        /// 
        /// </summary>
        Submitted = 1,
        /// <summary>
        /// 
        /// </summary>
        Modified = 2,
        /// <summary>
        /// 
        /// </summary>
        Removed = 4,
        /// <summary>
        /// 
        /// </summary>
        Approving = 8,
        /// <summary>
        /// 
        /// </summary>
        Approved = 16,
        /// <summary>
        /// 
        /// </summary>
        Rejected = 32,
    }

    /// <summary>
    /// 
    /// </summary>
    public enum CascadeMode
    {
        /// <summary>
        /// 
        /// </summary>
        NotSet,
        /// <summary>
        /// 
        /// </summary>
        All,
        /// <summary>
        /// 
        /// </summary>
        Specified,
        /// <summary>
        /// 
        /// </summary>
        Never
    }

    /// <summary>
    /// 
    /// </summary>
    public class EventSetting
    {
        /// <summary>
        /// 
        /// </summary>
        public EventType EventType { get; set; }
        /// <summary>
        /// 节点流转时节点ID
        /// </summary>
        public string? WfNodeId { get; set; }
        /// <summary>
        /// 节点流转时节点操作，提交或退回
        /// </summary>
        public string? NodeAction { get; set; }

        /// <summary>
        /// 触发表单
        /// </summary>
        public string? SourceFormId { get; set; }
        /// <summary>
        /// 相关表单
        /// </summary>
        public List<string>? OtherFormIds { get; set; }
        /// <summary>
        /// 级联触发模式
        /// </summary>
        public CascadeMode CascadeMode { get; set; }
        /// <summary>
        /// 指定触发的DataflowId
        /// </summary>
        public string? SpecifiedEvents { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    public enum UpdateMode
    {
        /// <summary>
        /// 
        /// </summary>
        Form,
        /// <summary>
        /// 
        /// </summary>
        Node,
    }

    /// <summary>
    /// 
    /// </summary>
    public enum ApproveAction
    {
        /// <summary>
        /// 
        /// </summary>
        None = 0,
        /// <summary>
        /// 
        /// </summary>
        Approve = 1,
        /// <summary>
        /// 
        /// </summary>
        Reject = 2,
        /// <summary>
        /// 
        /// </summary>
        Return = 3,
        /// <summary>
        /// 
        /// </summary>
        AddSignPre = 4,
        /// <summary>
        /// 
        /// </summary>
        AddSignAfter = 5,
        /// <summary>
        /// 
        /// </summary>
        AutoApprove = 6,
        /// <summary>
        /// 
        /// </summary>
        CopyTo = 7,

    }
}
