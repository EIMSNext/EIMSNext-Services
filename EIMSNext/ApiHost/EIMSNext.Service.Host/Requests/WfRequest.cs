using EIMSNext.ApiClient.Flow;

namespace EIMSNext.Service.Host.Requests
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

    public class WfReturnRequest
    {
        public string WfInstanceId { get; set; } = string.Empty;
        public string DataId { get; set; } = string.Empty;
        public string WfNodeId { get; set; } = string.Empty;
        public string TargetNodeId { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
    }

    public class WfTransferRequest
    {
        public string WfInstanceId { get; set; } = string.Empty;
        public string DataId { get; set; } = string.Empty;
        public string WfNodeId { get; set; } = string.Empty;
        public string TargetEmployeeId { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
    }

    public class WfAddSignRequest
    {
        public string WfInstanceId { get; set; } = string.Empty;
        public string DataId { get; set; } = string.Empty;
        public string WfNodeId { get; set; } = string.Empty;
        public string TargetEmployeeId { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
    }

    public class WfWithdrawRequest
    {
        public string WfInstanceId { get; set; } = string.Empty;
        public string DataId { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
    }

    public class WfUrgeRequest
    {
        public string WfInstanceId { get; set; } = string.Empty;
        public string DataId { get; set; } = string.Empty;
    }

    public class WfActionStatusRequest
    {
        public string WfInstanceId { get; set; } = string.Empty;
        public string DataId { get; set; } = string.Empty;
    }
}
