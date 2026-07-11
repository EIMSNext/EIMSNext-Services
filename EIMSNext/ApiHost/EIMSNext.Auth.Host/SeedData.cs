using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Integrations.Abstractions;

namespace EIMSNext.Auth.Host
{
    public class SeedData
    {
        public static IEnumerable<Auth.Entities.Client> GetClients(IConfiguration configuration)
        {
            return
            [
                new Auth.Entities.Client
                {
                    Id = Auth.Entities.InternalClients.WebClientId,
                    Name = "EIMSNext.Web",
                    RequireClientSecret = false,
                    AllowedGrantTypes =
                    [
                        new Auth.Entities.ClientGrantType { GrantType = "password" },
                        new Auth.Entities.ClientGrantType { GrantType = Auth.Entities.CustomGrantType.VerificationCode },
                        new Auth.Entities.ClientGrantType { GrantType = Auth.Entities.CustomGrantType.SingleSignOn }
                    ],
                    AllowedScopes =
                    [
                        new Auth.Entities.ClientScope { Scope = "openid" },
                        new Auth.Entities.ClientScope { Scope = "profile" },
                        new Auth.Entities.ClientScope { Scope = "api.readwrite" }
                    ],
                    AccessTokenLifetime=Auth.Constants.TokenLifetime_Default,
                    IdentityTokenLifetime=Auth.Constants.TokenLifetime_Default
                },
                new Auth.Entities.Client
                {
                    Id = Auth.Entities.InternalClients.PublicClientId,
                    Name = "EIMSNext.Public",
                    RequireClientSecret = false,
                    AllowedGrantTypes =
                    [
                        new Auth.Entities.ClientGrantType { GrantType = Auth.Entities.CustomGrantType.Public }
                    ],
                    AllowedScopes =
                    [
                        new Auth.Entities.ClientScope { Scope = nameof(EIMSNext.ApiService.PublicScope.DashLink) },
                        new Auth.Entities.ClientScope { Scope = nameof(EIMSNext.ApiService.PublicScope.FormLink) },
                        new Auth.Entities.ClientScope { Scope = nameof(EIMSNext.ApiService.PublicScope.DataLink) },
                        new Auth.Entities.ClientScope { Scope = nameof(EIMSNext.ApiService.PublicScope.QueryLink) },
                        new Auth.Entities.ClientScope { Scope = ((int)EIMSNext.ApiService.PublicScope.DashLink).ToString() },
                        new Auth.Entities.ClientScope { Scope = ((int)EIMSNext.ApiService.PublicScope.FormLink).ToString() },
                        new Auth.Entities.ClientScope { Scope = ((int)EIMSNext.ApiService.PublicScope.DataLink).ToString() },
                        new Auth.Entities.ClientScope { Scope = ((int)EIMSNext.ApiService.PublicScope.QueryLink).ToString() }
                    ],
                    AccessTokenLifetime = Auth.Constants.TokenLifetime_Default,
                    IdentityTokenLifetime = Auth.Constants.TokenLifetime_Default
                },
                new Auth.Entities.Client
                {
                    Id = Auth.Entities.InternalClients.SystemClientId,
                    Name = "EIMSNext.System",
                    RequireClientSecret = true,
                    ClientSecrets =
                    [
                        new Auth.Entities.ClientSecret
                        {
                            Type = "SharedSecret",
                            Value = Auth.Entities.InternalClients.SystemClientSecret.Sha256()
                        }
                    ],
                    AllowedGrantTypes =
                    [
                        new Auth.Entities.ClientGrantType { GrantType = Auth.Entities.CustomGrantType.System }
                    ],
                    AllowedScopes =
                    [
                        new Auth.Entities.ClientScope { Scope = "api.readwrite" }
                    ],
                    AccessTokenLifetime = Auth.Constants.TokenLifetime_Default,
                    IdentityTokenLifetime = Auth.Constants.TokenLifetime_Default
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
