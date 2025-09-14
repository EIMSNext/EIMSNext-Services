using EIMSNext.ApiClient.Flow;

namespace EIMSNext.ServiceApi.Request
{
    /// <summary>
    /// 
    /// </summary>
    public class WfStartRequest
    {
        /// <summary>
        /// 
        /// </summary>
        public string DataId { get; set; } = string.Empty;
    }
    /// <summary>
    /// 
    /// </summary>
    public class WfApproveRequest
    {
        /// <summary>
        /// 
        /// </summary>
        public string DataId { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public ApproveAction Action { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string Comment { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public string Signature { get; set; } = string.Empty;
    }
}
