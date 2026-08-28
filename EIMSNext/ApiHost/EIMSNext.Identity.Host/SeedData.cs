using EIMSNext.Identity.Abstractions;
using EIMSNext.Entities;

namespace EIMSNext.Identity.Host
{
    public class SeedData
    {
        public static IEnumerable<Client> GetClients(IConfiguration configuration)
        {
            return
            [
                new Client
                {
                    Id = InternalClients.WebClientId,
                    Name = "EIMSNext.Web",
                    RequireClientSecret = false,
                    AllowedGrantTypes =
                    [
                        new ClientGrantType { GrantType = "password" },
                        new ClientGrantType { GrantType = CustomGrantType.VerificationCode },
                        new ClientGrantType { GrantType = CustomGrantType.SingleSignOn },
                        new ClientGrantType { GrantType = CustomGrantType.Integration }
                    ],
                    AllowedScopes =
                    [
                        new ClientScope { Scope = "openid" },
                        new ClientScope { Scope = "profile" },
                        new ClientScope { Scope = "api.readwrite" }
                    ],
                    AccessTokenLifetime = Constants.TokenLifetime_Default,
                    IdentityTokenLifetime = Constants.TokenLifetime_Default
                },
                new Client
                {
                    Id = InternalClients.PublicClientId,
                    Name = "EIMSNext.Public",
                    RequireClientSecret = false,
                    AllowedGrantTypes =
                    [
                        new ClientGrantType { GrantType = CustomGrantType.Public }
                    ],
                    AllowedScopes =
                    [
                        new ClientScope { Scope = nameof(EIMSNext.ApiService.PublicScope.DashLink) },
                        new ClientScope { Scope = nameof(EIMSNext.ApiService.PublicScope.FormLink) },
                        new ClientScope { Scope = nameof(EIMSNext.ApiService.PublicScope.DataLink) },
                        new ClientScope { Scope = nameof(EIMSNext.ApiService.PublicScope.QueryLink) }
                    ],
                    AccessTokenLifetime = Constants.TokenLifetime_Default,
                    IdentityTokenLifetime = Constants.TokenLifetime_Default
                },
                new Client
                {
                    Id = InternalClients.SystemClientId,
                    Name = "EIMSNext.System",
                    RequireClientSecret = true,
                    ClientSecrets =
                    [
                        new ClientSecret
                        {
                            Type = "SharedSecret",
                            Value = InternalClients.SystemClientSecret.Sha256()
                        }
                    ],
                    AllowedGrantTypes =
                    [
                        new ClientGrantType { GrantType = CustomGrantType.System }
                    ],
                    AllowedScopes =
                    [
                        new ClientScope { Scope = "api.readwrite" }
                    ],
                    AccessTokenLifetime = Constants.TokenLifetime_Default,
                    IdentityTokenLifetime = Constants.TokenLifetime_Default
                }
            ];
        }

        public static List<User> GetUsers()
        {
            return new List<User>
            {
                //new User {Id="system", Name = "System" },
                //new User {Id="anonymous", Name = "Anonymous" },
                new User { Id = "admin", Name = "Admin", Password = HKH.Common.Security.BCrypt.HashPassword("123456"), Email = "admin@eimsnext.com", Phone = "12345678901" },
                new User
                {
                    Id = "cloudadmin",
                    Name = "Cloud Admin",
                    Password = HKH.Common.Security.BCrypt.HashPassword("123456"),
                    Email = "cloudadmin@easyun.cn",
                    UserType = "platadmin"
                }
            };
        }

        public static IEnumerable<IntegrationLoginSetting> GetIntegrationLoginSettings()
        {
            return
            [
                new IntegrationLoginSetting
                {
                    Id = IntegrationLoginType.WeChat,
                    Type = IntegrationLoginType.WeChat,
                    DisplayName = "微信",
                    Enabled = false
                },
                new IntegrationLoginSetting
                {
                    Id = IntegrationLoginType.WxWork,
                    Type = IntegrationLoginType.WxWork,
                    DisplayName = "企业微信",
                    Enabled = false
                },
                new IntegrationLoginSetting
                {
                    Id = IntegrationLoginType.DingTalk,
                    Type = IntegrationLoginType.DingTalk,
                    DisplayName = "钉钉",
                    Enabled = false
                },
                new IntegrationLoginSetting
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
