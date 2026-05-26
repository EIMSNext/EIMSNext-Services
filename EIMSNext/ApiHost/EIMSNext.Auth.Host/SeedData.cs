using EIMSNext.Auth.Integrations.Abstractions;

namespace EIMSNext.Auth.Host
{
    public class SeedData
    {
        public static IEnumerable<Auth.Entities.Client> GetClients()
        {
            return
            [
                new Auth.Entities.Client
                {
                    Id = Auth.Constants.ClientId_Web,
                    ClientName = "EIMSNext.Web",
                    RequireClientSecret = false,
                    AllowedGrantTypes =
                    [
                        new Auth.Entities.ClientGrantType { GrantType = "password" },
                        new Auth.Entities.ClientGrantType { GrantType = Auth.Entities.CustomGrantType.VerificationCode },
                        new Auth.Entities.ClientGrantType { GrantType = Auth.Entities.CustomGrantType.SingleSignOn },
                        new Auth.Entities.ClientGrantType { GrantType = Auth.Entities.CustomGrantType.Integration }
                    ],
                    AllowedScopes =
                    [
                        new Auth.Entities.ClientScope { Scope = "openid" },
                        new Auth.Entities.ClientScope { Scope = "profile" },
                        new Auth.Entities.ClientScope { Scope = "api.readwrite" }
                    ],
                    AccessTokenLifetime=Auth.Constants.TokenLifetime_Default,
                    IdentityTokenLifetime=Auth.Constants.TokenLifetime_Default
                }
            ];
        }

        public static List<Auth.Entities.User> GetUsers()
        {
            return new List<Auth.Entities.User>
            {
                //new Auth.Entities.User {Id="system", Name = "System" },
                //new Auth.Entities.User {Id="anonymous", Name = "Anonymous" },
                new Auth.Entities.User {Id="admin", Name = "Admin", Password = HKH.Common.Security.BCrypt.HashPassword("123456"), Email = "admin@eimsnext.com", Phone = "12345678901" }
            };
        }

        public static IEnumerable<Auth.Entities.IntegrationLoginSetting> GetIntegrationLoginSettings()
        {
            return
            [
                new Auth.Entities.IntegrationLoginSetting
                {
                    Id = IntegrationLoginType.WeChat,
                    Type = IntegrationLoginType.WeChat,
                    DisplayName = "微信",
                    Enabled = false
                },
                new Auth.Entities.IntegrationLoginSetting
                {
                    Id = IntegrationLoginType.WxWork,
                    Type = IntegrationLoginType.WxWork,
                    DisplayName = "企业微信",
                    Enabled = false
                },
                new Auth.Entities.IntegrationLoginSetting
                {
                    Id = IntegrationLoginType.DingTalk,
                    Type = IntegrationLoginType.DingTalk,
                    DisplayName = "钉钉",
                    Enabled = false
                },
                new Auth.Entities.IntegrationLoginSetting
                {
                    Id = IntegrationLoginType.Feishu,
                    Type = IntegrationLoginType.Feishu,
                    DisplayName = "飞书",
                    Enabled = false
                }
            ];
        }
    }
}
