namespace EIMSNext.Auth.Entities
{
    /// <summary>
    /// 第三方集成登录类型。
    /// </summary>
    public static class IntegrationLoginType
    {
        /// <summary>
        /// 微信开放平台扫码登录。
        /// </summary>
        public const string WeChat = "wechat";

        /// <summary>
        /// 企业微信登录。
        /// </summary>
        public const string WxWork = "wxwork";

        /// <summary>
        /// 钉钉登录。
        /// </summary>
        public const string DingTalk = "dingding";

        /// <summary>
        /// 飞书登录。
        /// </summary>
        public const string Feishu = "feishu";
    }
}
