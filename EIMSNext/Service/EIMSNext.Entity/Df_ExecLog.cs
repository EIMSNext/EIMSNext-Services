using EIMSNext.Core.Entity;

namespace EIMSNext.Entity
{
    /// <summary>
    /// 
    /// </summary>
    public class Df_ExecLog : MongoEntityBase
    {
        /// <summary>
        /// 
        /// </summary>
        public string WfInstanceId { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public string DataId { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public string NodeId { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public bool Success { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string ErrMsg { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public DateTime ExecTime { get; set; }
    }
}
