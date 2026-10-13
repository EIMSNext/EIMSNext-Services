namespace EIMSNext.Entities
{
    public static class CustomGrantType
    {
        public const string VerificationCode = "verification_code";
        public const string SingleSignOn = "sso_credentials";
        public const string Integration = "integration";
        public const string Public = "public";
        public const string System = "system";

        /// <summary>
        /// 标准 OAuth2 <c>client_credentials</c> grant：第三方应用以 ClientId/ClientSecret 直接换取 token，
        /// 不绑定任何用户。token 中的 <c>sub</c> 和 <c>client_id</c> 都指向 Client 自身。
        /// </summary>
        public const string ClientCredentials = "client_credentials";

        /// <summary>
        /// 授权码换 token：portal 密码登录签发一次性 code（回传时加 <c>lg_</c> 前缀），
        /// 或第三方集成登录回传的第三方 code（加 <c>wx_/ww_/dd_/fs_</c> 前缀）。
        /// 调用方以 <c>authorization_code</c> grant 换取 token，后端按前缀完成换取，
        /// 避免 access_token 与第三方 token 出现在回跳 URL 上。
        /// </summary>
        public const string AuthorizationCode = "authorization_code";

        /// <summary>
        /// <see cref="AuthorizationCode"/> 的 OpenIddict 管线内部视图名。authorization_code 是
        /// OAuth2 标准名：以其注册 custom flow 会被 OpenIddict 拒绝，按标准流声明又强制要求
        /// 授权端点与授权请求校验（本站授权码全部由 web/token MVC 端点直接处理，
        /// 不走浏览器授权码流程）。ExtractTokenRequest 管线把 <c>grant_type=authorization_code</c>
        /// 改写为该别名参与 OpenIddict 校验；真实 grant_type 只存在于 encrypted 载荷中，
        /// 由 MVC 端点按前缀分发。
        /// </summary>
        public const string AuthorizationCodeAlias = "eims_authorization_code";
    }
}
