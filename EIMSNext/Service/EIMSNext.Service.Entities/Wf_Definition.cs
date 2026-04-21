using EIMSNext.Common;
using EIMSNext.Core.Entities;
using EIMSNext.Plugin.Contracts;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 工作流定义实体，包含流程配置、版本信息及元数据
    /// </summary>
    public class Wf_Definition : CorpEntityBase
    {
        /// <summary>
        /// 应用ID
        /// </summary>
        public string AppId { get; set; } = string.Empty;
        /// <summary>
        /// 工作流名称
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// 流程类型（工作流或数据流）
        /// </summary>
        public FlowType FlowType { get; set; } = FlowType.Workflow;
        /// <summary>
        /// 表单ID
        /// </summary>
        public string ExternalId { get; set; } = string.Empty;
        /// <summary>
        /// 工作流描述
        /// </summary>
        public string Description { get; set; } = string.Empty;
        /// <summary>
        /// 版本号
        /// </summary>
        public int Version { get; set; }
        /// <summary>
        /// 是否为当前生效版本
        /// </summary>
        public bool IsCurrent { get; set; }
        /// <summary>
        /// 是否已发布过，发布后仅允许修改节点配置
        /// </summary>
        public bool Released { get; set; }
        /// <summary>
        /// 工作流定义内容（JSON格式的流程配置）
        /// </summary>
        public string Content { get; set; } = string.Empty;
        /// <summary>
        /// 工作流元数据，包含步骤、错误处理等配置
        /// </summary>
        public WfMetadata Metadata { get; set; } = new WfMetadata();

        //Dataflow
        /// <summary>
        /// 事件来源类型（表单或按钮）
        /// </summary>
        public EventSourceType EventSource { get; set; }
        /// <summary>
        /// 来源对象ID
        /// </summary>
        public string? SourceId { get; set; }
        /// <summary>
        /// 事件触发设置
        /// </summary>
        public EventSetting? EventSetting { get; set; }
        /// <summary>
        /// 是否禁用
        /// </summary>
        public bool Disabled { get; set; }
    }

    /// <summary>
    /// 工作流元数据，包含流程步骤和错误处理配置
    /// </summary>
    public class WfMetadata
    {
        /// <summary>
        /// 元数据唯一标识
        /// </summary>
        public string Id { get; set; } = string.Empty;
        /// <summary>
        /// 元数据版本号
        /// </summary>
        public int Version { get; set; }

        //public string DataType { get; set; }

        //执行失败后，任务挂起
        //public WorkflowErrorHandling DefaultErrorBehavior { get; set; }
        /// <summary>
        /// 默认错误处理行为（1表示任务挂起）
        /// </summary>
        public int DefaultErrorBehavior { get; set; } = 1;
        /// <summary>
        /// 默认错误重试间隔时间
        /// </summary>
        public TimeSpan? DefaultErrorRetryInterval { get; set; }
        /// <summary>
        /// 工作流步骤列表
        /// </summary>
        public List<WfStep> Steps { get; set; } = new List<WfStep>();
    }

    /// <summary>
    /// 工作流步骤节点定义
    /// </summary>
    public class WfStep
    {
        /// <summary>
        /// 节点类型（开始、结束、审批、分支等）
        /// </summary>
        public WfNodeType NodeType { get; set; }
        /// <summary>
        /// 步骤类型标识
        /// </summary>
        public string StepType { get; set; } = string.Empty;
        /// <summary>
        /// 步骤唯一标识
        /// </summary>
        public string Id { get; set; } = string.Empty;
        /// <summary>
        /// 步骤名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        //public string CancelCondition { get; set; }

        //public WorkflowErrorHandling? ErrorBehavior { get; set; }
        /// <summary>
        /// 错误处理行为，为空时使用默认设置
        /// </summary>
        public int? ErrorBehavior { get; set; }
        /// <summary>
        /// 错误重试间隔时间
        /// </summary>
        public TimeSpan? RetryInterval { get; set; }
        /// <summary>
        /// 并行工作步骤列表（用于分支处理）
        /// </summary>
        public List<List<WfStep>> Work { get; set; } = new List<List<WfStep>>();
        /// <summary>
        /// 补偿步骤列表（用于Saga模式回滚）
        /// </summary>
        public List<WfStep> CompensateWith { get; set; } = new List<WfStep>();
        /// <summary>
        /// 是否启用Saga分布式事务模式
        /// </summary>
        public bool Saga { get; set; } = false;
        /// <summary>
        /// 下一步骤ID
        /// </summary>
        public string NextStepId { get; set; } = string.Empty;

        //public ExpandoObject Inputs { get; set; } = new ExpandoObject();

        //public Dictionary<string, string> Outputs { get; set; } = new Dictionary<string, string>();
        /// <summary>
        /// 条件分支下一步骤映射（条件表达式->步骤ID）
        /// </summary>
        public Dictionary<string, string> SelectNextStep { get; set; } = new Dictionary<string, string>();
        /// <summary>
        /// 工作流节点设置
        /// </summary>
        public WfNodeSetting? WfNodeSetting { get; set; }
        /// <summary>
        /// 数据流节点设置
        /// </summary>
        public DfNodeSetting? DfNodeSetting { get; set; }
    }

    /// <summary>
    /// 工作流节点设置，包含审批和抄送配置
    /// </summary>
    public class WfNodeSetting
    {
        /// <summary>
        /// 节点类型
        /// </summary>
        public WfNodeType NodeType { get; set; }
        /// <summary>
        /// 审批设置
        /// </summary>
        public ApproveSetting? ApproveSetting { get; set; }
        /// <summary>
        /// 抄送设置
        /// </summary>
        public CopyToSetting? CopyToSetting { get; set; }
    }
    /// <summary>
    /// 审批节点设置，包含审批模式、候选人等信息
    /// </summary>
    public class ApproveSetting
    {
        /// <summary>
        /// 审批模式（或签、会签、自动签）
        /// </summary>
        public WfApprovalMode? ApprovalMode { get; set; }
        /// <summary>
        /// 审批候选人列表
        /// </summary>
        public IList<ApprovalCandidate>? Candidates { get; set; }
        /// <summary>
        /// 是否启用抄送
        /// </summary>
        public bool? EnableCopyto { get; set; }
        /// <summary>
        /// 抄送候选人列表
        /// </summary>
        public List<ApprovalCandidate>? CopytoCandidates { get; set; }
        /// <summary>
        /// 待办创建通知通道
        /// </summary>
        public NotifyChannel NotifyChannels { get; set; } = NotifyChannel.None;
        /// <summary>
        /// 超时处理设置
        /// </summary>
        public ExpireSetting? ExpireSetting { get; set; }
    }

    /// <summary>
    /// 超时处理设置
    /// </summary>
    public class ExpireSetting
    {
        /// <summary>
        /// 超时处理方式
        /// </summary>
        public WfExpireActionType ActionType { get; set; } = WfExpireActionType.AutoNotify;
        /// <summary>
        /// 超时数值
        /// </summary>
        public int TimeValue { get; set; }
        /// <summary>
        /// 超时时间单位
        /// </summary>
        public TimeUnit TimeUnit { get; set; } = TimeUnit.Minute;
        /// <summary>
        /// 超时提醒设置
        /// </summary>
        public NotifySetting? NotifySetting { get; set; }
        /// <summary>
        /// 超时转交设置
        /// </summary>
        public TransferSetting? TransferSetting { get; set; }
    }

    /// <summary>
    /// 通知设置
    /// </summary>
    public class NotifySetting
    {
        /// <summary>
        /// 通知通道
        /// </summary>
        public NotifyChannel Channels { get; set; } = NotifyChannel.None;
        /// <summary>
        /// 通知候选人
        /// </summary>
        public List<ApprovalCandidate>? Candidates { get; set; }
    }

    /// <summary>
    /// 转交设置
    /// </summary>
    public class TransferSetting
    {
        /// <summary>
        /// 转交目标候选人
        /// </summary>
        public List<ApprovalCandidate>? Candidates { get; set; }
    }

    /// <summary>
    /// 超时处理动作
    /// </summary>
    public enum WfExpireActionType
    {
        /// <summary>
        /// 自动提醒
        /// </summary>
        AutoNotify,
        /// <summary>
        /// 自动通过
        /// </summary>
        AutoApprove,
        /// <summary>
        /// 自动转交
        /// </summary>
        AutoTransfer,
        /// <summary>
        /// 自动驳回
        /// </summary>
        AutoReject,
        /// <summary>
        /// 自动回退
        /// </summary>
        AutoReturn
    }

    /// <summary>
    /// 时间单位
    /// </summary>
    public enum TimeUnit
    {
        /// <summary>
        /// 分钟
        /// </summary>
        Minute,
        /// <summary>
        /// 小时
        /// </summary>
        Hour,
        /// <summary>
        /// 天
        /// </summary>
        Day
    }

    /// <summary>
    /// 抄送节点设置，指定抄送对象
    /// </summary>
    public class CopyToSetting
    {
        /// <summary>
        /// 抄送候选人列表
        /// </summary>
        public IList<ApprovalCandidate>? Candidates { get; set; }
    }

    /// <summary>
    /// 数据流节点设置，包含数据操作相关配置
    /// </summary>
    public class DfNodeSetting
    {
        /// <summary>
        /// 节点类型
        /// </summary>
        public WfNodeType NodeType { get; set; }
        /// <summary>
        /// 是否仅返回单条结果
        /// </summary>
        public bool SingleResult { get; set; }
        /// <summary>
        /// 触发器设置
        /// </summary>
        public TriggerSetting? TriggerSetting { get; set; }
        /// <summary>
        /// 数据插入设置
        /// </summary>
        public InsertSetting? InsertSetting { get; set; }
        /// <summary>
        /// 数据更新设置
        /// </summary>
        public UpdateSetting? UpdateSetting { get; set; }
        /// <summary>
        /// 数据删除设置
        /// </summary>
        public DeleteSetting? DeleteSetting { get; set; }
        /// <summary>
        /// 查询单条数据设置
        /// </summary>
        public QueryOneSetting? QueryOneSetting { get; set; }
        /// <summary>
        /// 查询多条数据设置
        /// </summary>
        public QueryManySetting? QueryManySetting { get; set; }
        /// <summary>
        /// 打印设置
        /// </summary>
        public PrintSetting? PrintSetting { get; set; }
        /// <summary>
        /// 插件设置
        /// </summary>
        public PluginSetting? PluginSetting { get; set; }
    }

    /// <summary>
    /// 触发器设置，定义事件触发条件和行为
    /// </summary>
    public class TriggerSetting
    {
        /// <summary>
        /// 事件类型（提交、修改、删除、审批等）
        /// </summary>
        public EventType? EventType { get; set; }
        /// <summary>
        /// 关联表单ID
        /// </summary>
        public string? FormId { get; set; }
        /// <summary>
        /// 触发变更的字段列表
        /// </summary>
        public List<string>? ChangeFields { get; set; }
        /// <summary>
        /// 触发条件表达式
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
    /// 数据插入设置，指定目标表单和字段映射
    /// </summary>
    public class InsertSetting
    {
        /// <summary>
        /// 目标表单ID
        /// </summary>
        public string FormId { get; set; } = string.Empty;
        /// <summary>
        /// 字段设置列表，定义字段映射关系
        /// </summary>
        public List<FormFieldSetting> FieldSettings { get; set; } = new List<FormFieldSetting>();
    }

    /// <summary>
    /// 表单字段定义
    /// </summary>
    public class FormField
    {
        /// <summary>
        /// 表单ID
        /// </summary>
        public string FormId { get; set; } = string.Empty;
        /// <summary>
        /// 字段名称
        /// </summary>
        public string Field { get; set; } = string.Empty;
        /// <summary>
        /// 字段类型（输入、输出等）
        /// </summary>
        public string Type { get; set; } = FieldType.Input;
        /// <summary>
        /// 是否为子表字段
        /// </summary>
        public bool IsSubField { get; set; }
        /// <summary>
        /// 关联的节点ID
        /// </summary>
        public string? NodeId { get; set; } = string.Empty;
    }

    /// <summary>
    /// 表单字段设置，定义字段值来源和映射
    /// </summary>
    public class FormFieldSetting
    {
        /// <summary>
        /// 字段定义
        /// </summary>
        public FormField Field { get; set; } = new FormField();
        /// <summary>
        /// 值类型（自定义值、字段引用、空值）
        /// </summary>
        public FieldValueType ValueType { get; set; }
        /// <summary>
        /// 值表达式
        /// </summary>
        public string ValueExp { get; set; } = string.Empty;
        /// <summary>
        /// 字段值设置（用于字段引用类型）
        /// </summary>
        public FormFieldValueSetting? ValueField { get; set; }
    }

    /// <summary>
    /// 表单字段值设置
    /// </summary>
    public class FormFieldValueSetting
    {
        /// <summary>
        /// 字段定义
        /// </summary>
        public FormField Field { get; set; } = new FormField();
        /// <summary>
        /// 是否为单结果节点
        /// </summary>
        public bool? SingleResultNode { get; set; }
    }

    /// <summary>
    /// 字段值类型枚举
    /// </summary>
    public enum FieldValueType
    {
        /// <summary>
        /// 自定义值
        /// </summary>
        Custom,
        /// <summary>
        /// 引用字段值
        /// </summary>
        Field,
        /// <summary>
        /// 公式
        /// </summary>
        Formula,
        /// <summary>
        /// 空值
        /// </summary>
        Empty
    }

    /// <summary>
    /// 数据更新设置
    /// </summary>
    public class UpdateSetting
    {
        /// <summary>
        /// 更新模式（按表单或按节点）
        /// </summary>
        public UpdateMode UpdateMode { get; set; }
        /// <summary>
        /// 关联节点ID
        /// </summary>
        public string? NodeId { get; set; }
        /// <summary>
        /// 目标表单ID
        /// </summary>
        public string FormId { get; set; } = string.Empty;
        /// <summary>
        /// 字段设置列表
        /// </summary>
        public List<FormFieldSetting> FieldSettings { get; set; } = new List<FormFieldSetting>();
        /// <summary>
        /// 更新时，两端对象的匹配条件，用于内存中计算
        /// </summary>
        public DataMatchSetting UpdateMatch { get; set; } = new DataMatchSetting();
        /// <summary>
        /// 动态查询选项（JSON格式）
        /// </summary>
        public string? DynamicFindOptions { get; set; }
        /// <summary>
        /// 未找到数据时是否执行插入
        /// </summary>
        public bool InsertIfNoData { get; set; }
        /// <summary>
        /// 插入时的字段设置（当InsertIfNoData为true时使用）
        /// </summary>
        public List<FormFieldSetting> InsertFieldSettings { get; set; } = new List<FormFieldSetting>();
    }

    /// <summary>
    /// 数据匹配条件设置，用于数据筛选和关联
    /// </summary>
    public class DataMatchSetting
    {
        /// <summary>
        /// 匹配字段
        /// </summary>
        public FormField Field { get; set; } = new FormField();
        /// <summary>
        /// FilterRel常量值
        /// </summary>
        public string? Rel { get; set; }
        /// <summary>
        /// 嵌套匹配条件列表（用于复杂条件组合）
        /// </summary>
        public List<DataMatchSetting>? Items { get; set; }

        /// <summary>
        /// FilterOp常量值
        /// </summary>
        public string? Op { get; set; }
        /// <summary>
        /// 匹配值设置
        /// </summary>
        public DataMatchValueSetting? Value { get; set; }
    }

    /// <summary>
    /// 数据匹配值设置
    /// </summary>
    public class DataMatchValueSetting
    {
        /// <summary>
        /// FieldValueType常量值
        /// </summary>
        public FieldValueType Type { get; set; }
        /// <summary>
        /// 匹配值
        /// </summary>
        public object? Value { get; set; }
        /// <summary>
        /// 字段值引用（当Type为Field时使用）
        /// </summary>
        public FormField? FieldValue { get; set; }
    }

    /// <summary>
    /// 数据删除设置
    /// </summary>
    public class DeleteSetting
    {
        /// <summary>
        /// 删除模式（按表单或按节点）
        /// </summary>
        public UpdateMode DeleteMode { get; set; }
        /// <summary>
        /// 关联节点ID
        /// </summary>
        public string? NodeId { get; set; }
        /// <summary>
        /// 目标表单ID
        /// </summary>
        public string FormId { get; set; } = string.Empty;
        /// <summary>
        /// 动态查询选项（JSON格式）
        /// </summary>
        public string? DynamicFindOptions { get; set; }
    }

    /// <summary>
    /// 查询单条数据设置
    /// </summary>
    public class QueryOneSetting
    {
        /// <summary>
        /// 目标表单ID
        /// </summary>
        public string FormId { get; set; } = string.Empty;
        /// <summary>
        /// 动态查询选项（JSON格式）
        /// </summary>
        public string? DynamicFindOptions { get; set; }
    }

    /// <summary>
    /// 查询多条数据设置
    /// </summary>
    public class QueryManySetting
    {
        /// <summary>
        /// 目标表单ID
        /// </summary>
        public string FormId { get; set; } = string.Empty;
        /// <summary>
        /// 动态查询选项（JSON格式）
        /// </summary>
        public string? DynamicFindOptions { get; set; }
    }

    /// <summary>
    /// 打印设置
    /// </summary>
    public class PrintSetting { }

    /// <summary>
    /// 流程类型枚举
    /// </summary>
    public enum FlowType
    {
        /// <summary>
        /// 工作流（审批流程）
        /// </summary>
        Workflow,
        /// <summary>
        /// 数据流（数据自动化处理流程）
        /// </summary>
        Dataflow
    }

    /// <summary>
    /// 工作流节点类型枚举
    /// </summary>
    public enum WfNodeType
    {
        /// <summary>
        /// 无
        /// </summary>
        None,
        /// <summary>
        /// 开始节点
        /// </summary>
        Start,
        /// <summary>
        /// 结束节点
        /// </summary>
        End,
        /// <summary>
        /// 分支节点
        /// </summary>
        Branch,
        /// <summary>
        /// 分支项
        /// </summary>
        BranchItem,
        /// <summary>
        /// 条件节点
        /// </summary>
        Condition,
        /// <summary>
        /// 其他条件（默认分支）
        /// </summary>
        ConditionOther,
        //工作流节点
        /// <summary>
        /// 审批节点
        /// </summary>
        Approve,
        /// <summary>
        /// 抄送节点
        /// </summary>
        CopyTo,
        //数据流节点
        /// <summary>
        /// 查询单条数据
        /// </summary>
        QueryOne,
        /// <summary>
        /// 查询多条数据
        /// </summary>
        QueryMany,
        /// <summary>
        /// 插入数据
        /// </summary>
        Insert,
        /// <summary>
        /// 更新数据
        /// </summary>
        Update,
        /// <summary>
        /// 删除数据
        /// </summary>
        Delete,
        /// <summary>
        /// 打印
        /// </summary>
        Print,
        /// <summary>
        /// 插件
        /// </summary>
        Plugin,
        /// <summary>
        /// 分支节点（第二种类型）
        /// </summary>
        Branch2,
    }

    /// <summary>
    /// 审批模式枚举
    /// </summary>
    public enum WfApprovalMode
    {
        /// <summary>
        /// 无
        /// </summary>
        None,
        /// <summary>
        /// 或签（任一人审批即可通过）
        /// </summary>
        OrSign,
        /// <summary>
        /// 会签（所有人都需审批通过）
        /// </summary>
        CounterSign,
        /// <summary>
        /// 自动签（系统自动审批）
        /// </summary>
        AutoSign
    }

    /// <summary>
    /// 审批候选人定义
    /// </summary>
    public class ApprovalCandidate
    {
        /// <summary>
        /// 候选人类型（部门、员工、角色等）
        /// </summary>
        public CandidateType CandidateType { get; set; }
        /// <summary>
        /// 候选人ID
        /// </summary>
        public string CandidateId { get; set; } = string.Empty;
        /// <summary>
        /// 候选人名称
        /// </summary>
        public string? CandidateName { get; set; }
        /// <summary>
        /// 是否级联部门（包含下级部门）
        /// </summary>
        public bool CascadedDept { get; set; }
    }

    /// <summary>
    /// 候选人类型枚举
    /// </summary>
    public enum CandidateType
    {
        /// <summary>
        /// 无
        /// </summary>
        None,
        /// <summary>
        /// 部门
        /// </summary>
        Department,
        /// <summary>
        /// 员工
        /// </summary>
        Employee,
        /// <summary>
        /// 角色
        /// </summary>
        Role,
        /// <summary>
        /// 动态计算
        /// </summary>
        Dynamic,
        /// <summary>
        /// 表单字段值
        /// </summary>
        FormField,
    }

    /// <summary>
    /// 事件来源类型枚举
    /// </summary>
    public enum EventSourceType
    {
        /// <summary>
        /// 无
        /// </summary>
        None,
        /// <summary>
        /// 表单
        /// </summary>
        Form,
        /// <summary>
        /// 按钮
        /// </summary>
        Buttton
    }

    /// <summary>
    /// 事件类型枚举（支持位运算组合）
    /// </summary>
    [Flags]
    public enum EventType
    {
        /// <summary>
        /// 无
        /// </summary>
        None = 0,
        /// <summary>
        /// 已提交
        /// </summary>
        Submitted = 1,
        /// <summary>
        /// 已修改
        /// </summary>
        Modified = 2,
        /// <summary>
        /// 已删除
        /// </summary>
        Removed = 4,
        /// <summary>
        /// 审批中
        /// </summary>
        Approving = 8,
        /// <summary>
        /// 已通过
        /// </summary>
        Approved = 16,
        /// <summary>
        /// 已驳回
        /// </summary>
        Rejected = 32,
    }

    /// <summary>
    /// 级联触发模式枚举
    /// </summary>
    public enum CascadeMode
    {
        /// <summary>
        /// 未设置
        /// </summary>
        NotSet,
        /// <summary>
        /// 全部级联
        /// </summary>
        All,
        /// <summary>
        /// 指定级联
        /// </summary>
        Specified,
        /// <summary>
        /// 不级联
        /// </summary>
        Never
    }

    /// <summary>
    /// 事件触发设置，定义数据流的触发条件和行为
    /// </summary>
    public class EventSetting
    {
        /// <summary>
        /// 事件类型
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
    /// 更新/删除模式枚举
    /// </summary>
    public enum UpdateMode
    {
        /// <summary>
        /// 按表单
        /// </summary>
        Form,
        /// <summary>
        /// 按节点
        /// </summary>
        Node,
    }

    /// <summary>
    /// 审批操作类型枚举
    /// </summary>
    public enum ApproveAction
    {
        /// <summary>
        /// 无
        /// </summary>
        None = 0,
        /// <summary>
        /// 审批通过
        /// </summary>
        Approve = 1,
        /// <summary>
        /// 驳回
        /// </summary>
        Reject = 2,
        /// <summary>
        /// 退回
        /// </summary>
        Return = 3,
        /// <summary>
        /// 加签（前置）
        /// </summary>
        AddSignPre = 4,
        /// <summary>
        /// 加签（后置）
        /// </summary>
        AddSignAfter = 5,
        /// <summary>
        /// 自动审批
        /// </summary>
        AutoApprove = 6,
        /// <summary>
        /// 抄送
        /// </summary>
        CopyTo = 7,

    }
}
