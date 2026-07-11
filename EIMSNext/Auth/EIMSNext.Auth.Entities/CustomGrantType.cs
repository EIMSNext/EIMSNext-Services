namespace EIMSNext.Auth.Entities
{
    public static class CustomGrantType
    {
        public const string VerificationCode = "verification_code";
        public const string SingleSignOn = "sso_credentials";
        public const string Public = "public";
        public const string System = "system";

        /// <summary>
        /// 标准 OAuth2 <c>client_credentials</c> grant：第三方应用以 ClientId/ClientSecret 直接换取 token，
        /// 不绑定任何用户。token 中的 <c>sub</c> 和 <c>client_id</c> 都指向 Client 自身。
        /// </summary>
        public const string ClientCredentials = "client_credentials";
    }
}
