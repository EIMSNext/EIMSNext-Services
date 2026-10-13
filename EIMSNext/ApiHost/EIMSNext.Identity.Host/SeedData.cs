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
                        "password",
                        CustomGrantType.VerificationCode,
                        CustomGrantType.SingleSignOn,
                        CustomGrantType.Integration,
                        CustomGrantType.AuthorizationCode
                    ],
                    AllowedScopes = ["openid", "profile", "api.readwrite"],
                    AccessTokenLifetime = Constants.TokenLifetime_Default,
                    IdentityTokenLifetime = Constants.TokenLifetime_Default
                },
                new Client
                {
                    Id = InternalClients.PublicClientId,
                    Name = "EIMSNext.Public",
                    RequireClientSecret = false,
                    AllowedGrantTypes = [CustomGrantType.Public],
                    AllowedScopes =
                    [
                        nameof(EIMSNext.ApiService.PublicScope.DashLink),
                        nameof(EIMSNext.ApiService.PublicScope.FormLink),
                        nameof(EIMSNext.ApiService.PublicScope.DataLink),
                        nameof(EIMSNext.ApiService.PublicScope.QueryLink)
                    ],
                    AccessTokenLifetime = Constants.TokenLifetime_Default,
                    IdentityTokenLifetime = Constants.TokenLifetime_Default
                },
                new Client
                {
                    Id = InternalClients.SystemClientId,
                    Name = "EIMSNext.System",
                    RequireClientSecret = true,
                    ClientSecret = InternalClients.SystemClientSecret.Sha256(),
                    AllowedGrantTypes = [CustomGrantType.System],
                    AllowedScopes = ["api.readwrite"],
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

