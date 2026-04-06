using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 工作流待办事项实体
    /// </summary>
    public class Wf_Todo : CorpEntityBase
    {
        /// <summary>
        /// 工作流实例ID
        /// </summary>
        public string WfInstanceId { get; set; } = string.Empty;
        /// <summary>
        /// 审批节点ID
        /// </summary>
        public string ApproveNodeId { get; set; } = string.Empty;
        /// <summary>
        /// 审批节点名称
        /// </summary>
        public string ApproveNodeName { get; set; } = string.Empty;
        /// <summary>
        /// 待办员工ID
        /// </summary>
        public string EmployeeId { get; set; } = string.Empty;
        /// <summary>
        /// 应用ID
        /// </summary>
        public string AppId { get; set; } = string.Empty;
        /// <summary>
        /// 表单ID
        /// </summary>
        public string FormId { get; set; } = string.Empty;
        /// <summary>
        /// 业务数据ID
        /// </summary>
        public string DataId { get; set; } = string.Empty;
        /// <summary>
        /// 表单类型
        /// </summary>
        public int FormType { get; set; } = 0;
        /// <summary>
        /// 流程发起人
        /// </summary>
        public Operator? Starter { get; set; }
        /// <summary>
        /// 审批节点开始时间（时间戳）
        /// </summary>
        public long ApproveNodeStartTime{ get; set; }
        /// <summary>
        /// 数据摘要字段列表
        /// </summary>
        public List<BriefField> DataBrief { get; set; } = new List<BriefField>();
    }

    /// <summary>
    /// 摘要字段，用于展示待办数据的关键信息
    /// </summary>
    public class BriefField
    {
        /// <summary>
        /// 字段名称
        /// </summary>
        public string Field { get; set; } = string.Empty;
        /// <summary>
        /// 字段显示标题
        /// </summary>
        public string Title { get; set; } = string.Empty;
        /// <summary>
        /// 字段值
        /// </summary>
        public object? Value { get; set; }
    }
}
