namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 流水号序列请求
    /// </summary>
    public class SerialNoSequenceRequest : RequestBase
    {
        /// <summary>
        /// 当前计数值。管理端手动重置时设为 0，下一次提交从 1 开始。
        /// </summary>
        public int? CurrId { get; set; }
    }
}
