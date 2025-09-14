using IdentityServer4;
using IdentityServer4.Models;

namespace EIMSNext.AuthApi
{
    public class SeedData
    {
        public static IEnumerable<Client> GetClients()
        {
            return new List<Client>
            {
                ///////////////////////////////////////////
                // Console Client Credentials Flow Sample
                //////////////////////////////////////////
                //new Client
                //{
                //    ClientId ="EF0451B3-CC8C-46D4-948E-E8FBC88F6980",
                //    ClientName = "service_200808081008",
                //    ClientSecrets =
                //    {
                //        new Secret("jacky@EIMSNext#service".Sha256())
                //    },
                //    AllowedGrantTypes = GrantTypes.ClientCredentials,
                //    AllowedScopes = {
                //        "api.readwrite" },
                //    AccessTokenLifetime=28800,
                //    IdentityTokenLifetime=28800
                //},

                 new Client
                {
                    ClientId =Auth.Constants.ClientId_Web,
                    ClientName = "EIMSNext.Web",
                    RequireClientSecret = false,
                    AllowedGrantTypes = {
                         GrantType.ResourceOwnerPassword,
                         Auth.Entity. CustomGrantType.VerificationCode,
                        Auth. Entity.CustomGrantType.SingleSignOn,
                        Auth. Entity.CustomGrantType.Integration},
                    AllowedScopes = {
                        IdentityServerConstants.StandardScopes.OpenId,
                        IdentityServerConstants.StandardScopes.Profile,
                        "api.readwrite"},
                    AccessTokenLifetime=Auth.Constants.TokenLifetime_Default,
                    IdentityTokenLifetime=Auth.Constants.TokenLifetime_Default
                },

                /////////////////////////////////////////////
                //// Console Client Credentials Flow with client JWT assertion
                ////////////////////////////////////////////
                //new Client
                //{
                //    ClientId = "client.jwt",
                //    ClientSecrets =
                //    {
                //        new Secret
                //        {
                //            Type = IdentityServerConstants.SecretTypes.X509CertificateBase64,
                //            Value = "MIIDATCCAe2gAwIBAgIQoHUYAquk9rBJcq8W+F0FAzAJBgUrDgMCHQUAMBIxEDAOBgNVBAMTB0RldlJvb3QwHhcNMTAwMTIwMjMwMDAwWhcNMjAwMTIwMjMwMDAwWjARMQ8wDQYDVQQDEwZDbGllbnQwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQDSaY4x1eXqjHF1iXQcF3pbFrIbmNw19w/IdOQxbavmuPbhY7jX0IORu/GQiHjmhqWt8F4G7KGLhXLC1j7rXdDmxXRyVJBZBTEaSYukuX7zGeUXscdpgODLQVay/0hUGz54aDZPAhtBHaYbog+yH10sCXgV1Mxtzx3dGelA6pPwiAmXwFxjJ1HGsS/hdbt+vgXhdlzud3ZSfyI/TJAnFeKxsmbJUyqMfoBl1zFKG4MOvgHhBjekp+r8gYNGknMYu9JDFr1ue0wylaw9UwG8ZXAkYmYbn2wN/CpJl3gJgX42/9g87uLvtVAmz5L+rZQTlS1ibv54ScR2lcRpGQiQav/LAgMBAAGjXDBaMBMGA1UdJQQMMAoGCCsGAQUFBwMCMEMGA1UdAQQ8MDqAENIWANpX5DZ3bX3WvoDfy0GhFDASMRAwDgYDVQQDEwdEZXZSb290ghAsWTt7E82DjU1E1p427Qj2MAkGBSsOAwIdBQADggEBADLje0qbqGVPaZHINLn+WSM2czZk0b5NG80btp7arjgDYoWBIe2TSOkkApTRhLPfmZTsaiI3Ro/64q+Dk3z3Kt7w+grHqu5nYhsn7xQFAQUf3y2KcJnRdIEk0jrLM4vgIzYdXsoC6YO+9QnlkNqcN36Y8IpSVSTda6gRKvGXiAhu42e2Qey/WNMFOL+YzMXGt/nDHL/qRKsuXBOarIb++43DV3YnxGTx22llhOnPpuZ9/gnNY7KLjODaiEciKhaKqt/b57mTEz4jTF4kIg6BP03MUfDXeVlM1Qf1jB43G2QQ19n5lUiqTpmQkcfLfyci2uBZ8BkOhXr3Vk9HIk/xBXQ="
                //        }
                //    },

                //    AllowedGrantTypes = GrantTypes.ClientCredentials,
                //    AllowedScopes = { "api1", "api2.read_only" }
                //},

                /////////////////////////////////////////////
                //// Custom Grant Sample
                ////////////////////////////////////////////
                //new Client
                //{
                //    ClientId = "client.custom",
                //    ClientSecrets =
                //    {
                //        new Secret("secret".Sha256())
                //    },

                //    AllowedGrantTypes = { "custom", "custom.nosubject" },
                //    AllowedScopes = { "api1", "api2.read_only" }
                //},

                /////////////////////////////////////////////
                //// Console Resource Owner Flow Sample
                ////////////////////////////////////////////
                //new Client
                //{
                //    ClientId = "roclient",
                //    ClientSecrets =
                //    {
                //        new Secret("secret".Sha256())
                //    },

                //    AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,

                //    AllowOfflineAccess = true,
                //    AllowedScopes =
                //    {
                //        IdentityServerConstants.StandardScopes.OpenId,
                //        "custom.profile",
                //        "api1", "api2.read_only"
                //    }
                //},

                /////////////////////////////////////////////
                //// Console Public Resource Owner Flow Sample
                ////////////////////////////////////////////
                //new Client
                //{
                //    ClientId = "roclient.public",
                //    RequireClientSecret = false,

                //    AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,

                //    AllowOfflineAccess = true,
                //    AllowedScopes =
                //    {
                //        IdentityServerConstants.StandardScopes.OpenId,
                //        IdentityServerConstants.StandardScopes.Email,
                //        "api1", "api2.read_only"
                //    }
                //},

                /////////////////////////////////////////////
                //// Console Hybrid with PKCE Sample
                ////////////////////////////////////////////
                //new Client
                //{
                //    ClientId = "console.hybrid.pkce",
                //    ClientName = "Console Hybrid with PKCE Sample",
                //    RequireClientSecret = false,

                //    AllowedGrantTypes = GrantTypes.Hybrid,
                //    RequirePkce = true,

                //    RedirectUris = { "http://127.0.0.1" },

                //    AllowOfflineAccess = true,

                //    AllowedScopes =
                //    {
                //        IdentityServerConstants.StandardScopes.OpenId,
                //        IdentityServerConstants.StandardScopes.Profile,
                //        IdentityServerConstants.StandardScopes.Email,
                //        "api1", "api2.read_only"
                //    }
                //},

                /////////////////////////////////////////////
                //// Introspection Client Sample
                ////////////////////////////////////////////
                //new Client
                //{
                //    ClientId = "roclient.reference",
                //    ClientSecrets =
                //    {
                //        new Secret("secret".Sha256())
                //    },

                //    AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,
                //    AllowedScopes = { "api1", "api2.read_only" },

                //    AccessTokenType = AccessTokenType.Reference
                //},

                /////////////////////////////////////////////
                //// MVC Implicit Flow Samples
                ////////////////////////////////////////////
                //new Client
                //{
                //    ClientId = "mvc.implicit",
                //    ClientName = "MVC Implicit",
                //    ClientUri = "http://identityserver.io",

                //    AllowedGrantTypes = GrantTypes.Implicit,
                //    AllowAccessTokensViaBrowser = true,

                //    RedirectUris =  { "http://localhost:44077/signin-oidc" },
                //    FrontChannelLogoutUri = "http://localhost:44077/signout-oidc",
                //    PostLogoutRedirectUris = { "http://localhost:44077/signout-callback-oidc" },

                //    AllowedScopes =
                //    {
                //        IdentityServerConstants.StandardScopes.OpenId,
                //        IdentityServerConstants.StandardScopes.Profile,
                //        IdentityServerConstants.StandardScopes.Email,
                //        "api1", "api2.read_only"
                //    }
                //},

                /////////////////////////////////////////////
                //// MVC Manual Implicit Flow Sample
                ////////////////////////////////////////////
                //new Client
                //{
                //    ClientId = "mvc.manual",
                //    ClientName = "MVC Manual",
                //    ClientUri = "http://identityserver.io",

                //    AllowedGrantTypes = GrantTypes.Implicit,

                //    RedirectUris = { "http://localhost:44078/home/callback" },
                //    FrontChannelLogoutUri = "http://localhost:44078/signout-oidc",
                //    PostLogoutRedirectUris = { "http://localhost:44078/" },

                //    AllowedScopes = { IdentityServerConstants.StandardScopes.OpenId }
                //},

                /////////////////////////////////////////////
                //// MVC Hybrid Flow Samples
                ////////////////////////////////////////////
                //new Client
                //{
                //    ClientId = "mvc.hybrid",
                //    ClientName = "MVC Hybrid",
                //    ClientUri = "http://identityserver.io",
                //    //LogoUri = "https://pbs.twimg.com/profile_images/1612989113/Ki-hanja_400x400.png",

                //    ClientSecrets =
                //    {
                //        new Secret("secret".Sha256())
                //    },

                //    AllowedGrantTypes = GrantTypes.Hybrid,
                //    AllowAccessTokensViaBrowser = false,

                //    RedirectUris = { "http://localhost:21402/signin-oidc" },
                //    FrontChannelLogoutUri = "http://localhost:21402/signout-oidc",
                //    PostLogoutRedirectUris = { "http://localhost:21402/signout-callback-oidc" },

                //    AllowOfflineAccess = true,

                //    AllowedScopes =
                //    {
                //        IdentityServerConstants.StandardScopes.OpenId,
                //        IdentityServerConstants.StandardScopes.Profile,
                //        IdentityServerConstants.StandardScopes.Email,
                //        "api1", "api2.read_only"
                //    }
                //},

                /////////////////////////////////////////////
                //// JS OAuth 2.0 Sample
                ////////////////////////////////////////////
                //new Client
                //{
                //    ClientId = "js_oauth",
                //    ClientName = "JavaScript OAuth 2.0 Client",
                //    ClientUri = "http://identityserver.io",
                //    //LogoUri = "https://pbs.twimg.com/profile_images/1612989113/Ki-hanja_400x400.png",

                //    AllowedGrantTypes = GrantTypes.Implicit,
                //    AllowAccessTokensViaBrowser = true,

                //    RedirectUris = { "http://localhost:28895/index.html" },
                //    AllowedScopes = { "api1", "api2.read_only" }
                //},
                
                /////////////////////////////////////////////
                //// JS OIDC Sample
                ////////////////////////////////////////////
                //new Client
                //{
                //    ClientId = "js_oidc",
                //    ClientName = "JavaScript OIDC Client",
                //    ClientUri = "http://identityserver.io",
                //    //LogoUri = "https://pbs.twimg.com/profile_images/1612989113/Ki-hanja_400x400.png",

                //    AllowedGrantTypes = GrantTypes.Implicit,
                //    AllowAccessTokensViaBrowser = true,
                //    RequireClientSecret = false,
                //    AccessTokenType = AccessTokenType.Jwt,

                //    RedirectUris =
                //    {
                //        "http://localhost:7017/index.html",
                //        "http://localhost:7017/callback.html",
                //        "http://localhost:7017/silent.html",
                //        "http://localhost:7017/popup.html"
                //    },

                //    PostLogoutRedirectUris = { "http://localhost:7017/index.html" },
                //    AllowedCorsOrigins = { "http://localhost:7017" },

                //    AllowedScopes =
                //    {
                //        IdentityServerConstants.StandardScopes.OpenId,
                //        IdentityServerConstants.StandardScopes.Profile,
                //        IdentityServerConstants.StandardScopes.Email,
                //        "api1", "api2.read_only", "api2.full_access"
                //    }
                //}
            };
        }

        public static IEnumerable<IdentityResource> GetIdentityResources()
        {
            return
            [
                // some standard scopes from the OIDC spec
                new IdentityResources.OpenId(),
                new IdentityResources.Profile(),
               // new IdentityResources.Email(),

                // custom identity resource with some consolidated claims
               // new IdentityResource("custom.profile", new[] { JwtClaimTypes.Name, JwtClaimTypes.Email, "location" })
            ];
        }

        public static IEnumerable<ApiResource> GetApiResources()
        {
            return
            [
                // simple version with ctor
                new ApiResource("eimsnext.api", "EIMSNext API")
                {
                    // this is needed for introspection when using reference tokens
                    ApiSecrets = { new Secret("eimsnext.api".Sha256()) },
                    Scopes = { "api.readonly", "api.readwrite" }
                },
                
                // expanded version if more control is needed
                //new ApiResource
                //{
                //    Name = "api2",

                //    ApiSecrets =
                //    {
                //        new Secret("secret".Sha256())
                //    },

                //    UserClaims =
                //    {
                //        JwtClaimTypes.Name,
                //        JwtClaimTypes.Email
                //    },

                //    Scopes =
                //    {
                //        "api2.full_access",
                //        "api2.read_only"
                //    }
                //}
            ];
        }

        public static IEnumerable<ApiScope> GetApiScopes()
        {
            return
            [
                new ApiScope()
                {
                    Name = "api.readwrite",
                    DisplayName = "Full access",
                },
                new ApiScope
                {
                    Name = "api.readonly",
                    DisplayName = "Read only access"
                }
            ];
        }

        public static List<Auth.Entity.User> GetUsers()
        {
            return new List<Auth.Entity.User>
            {
                //new Auth.Entity.User {Id="system", Name = "System" },
                //new Auth.Entity.User {Id="anonymous", Name = "Anonymous" },
                new Auth.Entity.User {Id="liwt.0918", Name = "Jacky", Password = HKH.Common.Security.BCrypt.HashPassword("123456"), Email = "liwt@123.com", Phone = "12345678901" }
            };
        }
    }
}
