namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 系统消息已读请求
    /// </summary>
    public class SystemMessageReadRequest
    {
        /// <summary>
        /// 消息ID
        /// </summary>
        public string Id { get; set; } = string.Empty;
    }
}
