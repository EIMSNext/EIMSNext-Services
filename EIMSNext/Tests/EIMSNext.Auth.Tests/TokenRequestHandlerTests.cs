using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Interfaces;
using EIMSNext.Auth.Models;
using EIMSNext.Auth.Services;
using EIMSNext.ApiService;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace EIMSNext.Auth.Tests
{
    [TestClass]
    public class TokenRequestHandlerTests
    {
        [TestMethod]
        public async Task HandleAsync_UsesClientScopes_WhenScopeMissing()
        {
            var user = new User
            {
                Id = "admin",
                Name = "Admin",
                Email = "admin@eimsnext.com",
                Phone = "12345678901",
                Password = "hashed",
                Crops = [new UserCorp { CorpId = "corp-001", IsDefault = true }]
            };

            var client = new Client
            {
                Id = InternalClients.WebClientId,
                Enabled = true,
                RequireClientSecret = false,
                AllowedGrantTypes =
                [
                    new ClientGrantType { GrantType = GrantTypes.Password }
                ],
                AllowedScopes =
                [
                    new ClientScope { Scope = "openid" },
                    new ClientScope { Scope = "profile" },
                    new ClientScope { Scope = "api.readwrite" }
                ]
            };

            var handler = new TokenRequestHandler(
                new FakeUserService(user, [client]),
                CreateGrantHandlers(user));

            var request = new OpenIddictRequest
            {
                ClientId = InternalClients.WebClientId,
                GrantType = GrantTypes.Password,
                Username = user.Email,
                Password = Constants.NoPassword
            };

            var result = await handler.HandleAsync(request);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(28800, result.AccessTokenLifetime);
            CollectionAssert.AreEqual(new[] { "openid", "profile", "api.readwrite" }, result.Scopes.ToArray());
            Assert.AreEqual("admin", result.Claims.Single(x => x.Type == AuthClaimTypes.Id).Value);
            Assert.AreEqual("corp-001", result.Claims.Single(x => x.Type == AuthClaimTypes.Corp).Value);
        }

        [TestMethod]
        public async Task HandleAsync_Fails_WhenRequestedScopeNotAllowed()
        {
            var user = new User
            {
                Id = "admin",
                Name = "Admin",
                Email = "admin@eimsnext.com",
                Password = "hashed",
                Crops = [new UserCorp { CorpId = "corp-001", IsDefault = true }]
            };

            var client = new Client
            {
                Id = InternalClients.WebClientId,
                Enabled = true,
                RequireClientSecret = false,
                AllowedGrantTypes = [new ClientGrantType { GrantType = GrantTypes.Password }],
                AllowedScopes = [new ClientScope { Scope = "api.readwrite" }]
            };

            var handler = new TokenRequestHandler(
                new FakeUserService(user, [client]),
                CreateGrantHandlers(user));

            var request = new OpenIddictRequest
            {
                ClientId = InternalClients.WebClientId,
                GrantType = GrantTypes.Password,
                Username = user.Email,
                Password = Constants.NoPassword,
                Scope = "openid"
            };

            var result = await handler.HandleAsync(request);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(Errors.InvalidScope, result.Error);
        }

        [TestMethod]
        public async Task HandleAsync_SystemGrant_ReturnsSystemClaims()
        {
            var client = new Client
            {
                Id = InternalClients.SystemClientId,
                Enabled = true,
                RequireClientSecret = true,
                ClientSecrets =
                [
                    new ClientSecret { Type = "SharedSecret", Value = InternalClients.SystemClientSecret.Sha256() }
                ],
                AllowedGrantTypes = [new ClientGrantType { GrantType = CustomGrantType.System }],
                AllowedScopes = [new ClientScope { Scope = "api.readwrite" }]
            };

            var auditLoginService = new FakeAuditLoginService();
            var handler = new TokenRequestHandler(
                new FakeUserService(new User { Id = "noop", Name = "noop" }, [client]),
                CreateGrantHandlers(new User { Id = "noop", Name = "noop" }, auditLoginService));

            var request = new OpenIddictRequest
            {
                ClientId = InternalClients.SystemClientId,
                ClientSecret = InternalClients.SystemClientSecret,
                GrantType = CustomGrantType.System,
                Scope = "api.readwrite"
            };
            request.SetParameter("corp_id", "corp-001");
            request.SetParameter("object_type", "wf");
            request.SetParameter("object_id", "wf-inst-001");

            var result = await handler.HandleAsync(request);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual("system", result.Claims.Single(x => x.Type == AuthClaimTypes.Id).Value);
            Assert.AreEqual("wf_wf-inst-001", result.Claims.Single(x => x.Type == AuthClaimTypes.Name).Value);
            Assert.AreEqual("corp-001", result.Claims.Single(x => x.Type == AuthClaimTypes.Corp).Value);
            Assert.AreEqual(IdentityType.System.ToString(), result.Claims.Single(x => x.Type == AuthClaimTypes.IdentityType).Value);
            Assert.AreEqual(0, auditLoginService.Entries.Count);
        }

        [TestMethod]
        public async Task HandleAsync_SystemGrant_Fails_WhenObjectInfoMissing()
        {
            var client = new Client
            {
                Id = InternalClients.SystemClientId,
                Enabled = true,
                RequireClientSecret = true,
                ClientSecrets =
                [
                    new ClientSecret { Type = "SharedSecret", Value = InternalClients.SystemClientSecret.Sha256() }
                ],
                AllowedGrantTypes = [new ClientGrantType { GrantType = CustomGrantType.System }],
                AllowedScopes = [new ClientScope { Scope = "api.readwrite" }]
            };

            var handler = new TokenRequestHandler(
                new FakeUserService(new User { Id = "noop", Name = "noop" }, [client]),
                CreateGrantHandlers(new User { Id = "noop", Name = "noop" }));

            var request = new OpenIddictRequest
            {
                ClientId = InternalClients.SystemClientId,
                ClientSecret = InternalClients.SystemClientSecret,
                GrantType = CustomGrantType.System,
                Scope = "api.readwrite"
            };
            request.SetParameter("corp_id", "corp-001");

            var result = await handler.HandleAsync(request);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(Errors.InvalidRequest, result.Error);
        }

        private static IHttpContextAccessor CreateHttpContextAccessor()
        {
            var context = new DefaultHttpContext();
            context.Request.Headers["X-Forwarded-For"] = new StringValues("127.0.0.1");
            return new HttpContextAccessor { HttpContext = context };
        }

        private static IReadOnlyList<ITokenGrantHandler> CreateGrantHandlers(User user, FakeAuditLoginService? auditLoginService = null)
        {
            var contextAccessor = CreateHttpContextAccessor();
            auditLoginService ??= new FakeAuditLoginService();
            return
            [
                new PasswordTokenGrantHandler(new FakeUserService(user), auditLoginService, contextAccessor),
                new VerificationCodeTokenGrantHandler(new FakeVerificationCodeService(), auditLoginService, contextAccessor),
                new SingleSignOnTokenGrantHandler(new FakeSingleSignOnService(), auditLoginService, contextAccessor),
                new SystemTokenGrantHandler(auditLoginService, contextAccessor)
            ];
        }

        private sealed class FakeUserService(User user, List<Client>? clients = null) : IUserService
        {
            public User? Validate(string emailOrPhone, string password)
            {
                return string.Equals(emailOrPhone, user.Email, StringComparison.OrdinalIgnoreCase) && password == Constants.NoPassword
                    ? user
                    : null;
            }

            public User? FindById(string id) => user.Id == id ? user : null;
            public User? FindByEmailOrPhone(string emailOrPhone) => user.Email == emailOrPhone || user.Phone == emailOrPhone ? user : null;
            public User? FindByEmail(string email) => user.Email == email ? user : null;
            public User? FindByPhone(string phone) => user.Phone == phone ? user : null;
            public User? FindByEmpNo(string corpId, string empNo) => null;
            public Client? FindEnabledClient(string clientId) => clients?.FirstOrDefault(x => x.Id == clientId && x.Enabled);
            public bool VerifyPassword(User inputUser, string password) => inputUser.Id == user.Id && password == Constants.NoPassword;
        }

        private sealed class FakeVerificationCodeService : IVerificationCodeService
        {
            public User? Validate(string? username, string? verifycode) => null;
        }

        private sealed class FakeSingleSignOnService : ISingleSignOnService
        {
            public User? Validate(string? corp_empno, string? secret) => null;
        }

        private sealed class FakeAuditLoginService : IAuditLoginService
        {
            public List<AuditLogin> Entries { get; } = [];

            public Task AddAuditLogin(AuditLogin entity)
            {
                Entries.Add(entity);
                return Task.CompletedTask;
            }
        }
    }
}
