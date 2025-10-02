using EIMSNext.Core.Entity;

namespace EIMSNext.Entity
{
    /// <summary>
    /// 审批日志
    /// </summary>
    public class Wf_ApprovalLog : CorpEntityBase
    {
        /// <summary>
        /// 
        /// </summary>
        public string AppId { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public string FormId { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public string DataId { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public int WfVersion { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string NodeId { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public string NodeName { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public WfNodeType NodeType { get; set; } = WfNodeType.None;
        /// <summary>
        /// 审批轮次
        /// </summary>
        public int Round { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public Operator? Approver { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public ApproveAction Result { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string? Comment { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string? Signature { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public DateTime ApprovalTime { get; set; }
    }
}
