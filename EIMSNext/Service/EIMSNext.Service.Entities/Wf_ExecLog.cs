using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 工作流执行日志实体
    /// </summary>
    public class Wf_ExecLog : MongoEntityBase
    {
        /// <summary>
        /// 工作流实例ID
        /// </summary>
        public string WfInstanceId { get; set; } = string.Empty;
        /// <summary>
        /// 业务数据ID
        /// </summary>
        public string DataId { get; set; } = string.Empty;
        /// <summary>
        /// 工作流节点ID
        /// </summary>
        public string NodeId { get; set; } = string.Empty;
        /// <summary>
        /// 执行人员ID
        /// </summary>
        public string EmpId { get; set; } = string.Empty;
        /// <summary>
        /// 执行是否成功
        /// </summary>
        public bool Success { get; set; }
        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrMsg { get; set; } = string.Empty;
        /// <summary>
        /// 执行耗时（毫秒）
        /// </summary>
        public long ExecTime { get; set; }
    }
}
