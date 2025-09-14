namespace EIMSNext.ApiClient.Abstraction
{
    public abstract class RestApiClientSetting : IApiClientSetting
    {
        /// <summary>
        /// 失败重试机制
        /// </summary>
        public RetryPolicy RetryPolicy { get; set; } = new RetryPolicy { Enabled = false };

        public string BaseUrl { get; set; } = string.Empty;
        /// <summary>
        /// 请求延迟时间，单位MS
        /// </summary>
        public int Delay { get; set; } = 0;
        public int? Timeout { get; set; }

        public abstract bool Verify();
    }

}
