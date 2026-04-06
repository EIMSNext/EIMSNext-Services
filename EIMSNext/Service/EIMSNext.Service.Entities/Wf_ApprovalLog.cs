using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 审批日志
    /// </summary>
    public class Wf_ApprovalLog : CorpEntityBase
    {
        /// <summary>
        /// 应用ID
        /// </summary>
        public string AppId { get; set; } = string.Empty;
        /// <summary>
        /// 表单ID
        /// </summary>
        public string FormId { get; set; } = string.Empty;
        /// <summary>
        /// 表单名称
        /// </summary>
        public string FormName { get; set; } = string.Empty;
        /// <summary>
        /// 数据ID
        /// </summary>
        public string DataId { get; set; } = string.Empty;
        /// <summary>
        /// 工作流版本
        /// </summary>
        public int WfVersion { get; set; }
        /// <summary>
        /// 节点ID
        /// </summary>
        public string NodeId { get; set; } = string.Empty;
        /// <summary>
        /// 节点名称
        /// </summary>
        public string NodeName { get; set; } = string.Empty;
        /// <summary>
        /// 节点类型
        /// </summary>
        public WfNodeType NodeType { get; set; } = WfNodeType.None;
        /// <summary>
        /// 审批轮次
        /// </summary>
        public int Round { get; set; }
        /// <summary>
        /// 审批人
        /// </summary>
        public Operator? Approver { get; set; }
        /// <summary>
        /// 审批结果
        /// </summary>
        public ApproveAction Result { get; set; }
        /// <summary>
        /// 审批意见
        /// </summary>
        public string? Comment { get; set; }
        /// <summary>
        /// 签名
        /// </summary>
        public string? Signature { get; set; }
        /// <summary>
        /// 审批时间戳
        /// </summary>
        public long ApprovalTime { get; set; }
        /// <summary>
        /// 数据摘要字段列表
        /// </summary>
        public List<BriefField> DataBrief { get; set; } = new List<BriefField>();
    }
}
