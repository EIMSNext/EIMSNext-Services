using EIMSNext.Core.Entity;

namespace EIMSNext.Entity
{
    /// <summary>
    /// 
    /// </summary>
    public class Wf_Todo : CorpEntityBase
    {
        /// <summary>
        /// 
        /// </summary>
        public string WfInstanceId { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public string ApproveNodeId { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public string ApproveNodeName { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public string EmployeeId { get; set; } = string.Empty;
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
        public int FormType { get; set; } = 0;
        /// <summary>
        /// 
        /// </summary>
        public Operator? Starter { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public long ApproveNodeStartTime{ get; set; }
        /// <summary>
        /// 
        /// </summary>
        public List<BriefField> DataBrief { get; set; } = new List<BriefField>();
    }

    /// <summary>
    /// 
    /// </summary>
    public class BriefField
    {
        /// <summary>
        /// 
        /// </summary>
        public string Field { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public string Title { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public object? Value { get; set; }
    }
}
