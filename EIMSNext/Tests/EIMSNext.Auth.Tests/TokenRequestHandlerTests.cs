using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Interfaces;
using EIMSNext.Auth.Services;
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
                Id = Constants.ClientId_Web,
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
                new FakeAuthDbContext([client], [user]),
                CreateGrantHandlers(user));

            var request = new OpenIddictRequest
            {
                ClientId = Constants.ClientId_Web,
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
                Id = Constants.ClientId_Web,
                Enabled = true,
                RequireClientSecret = false,
                AllowedGrantTypes = [new ClientGrantType { GrantType = GrantTypes.Password }],
                AllowedScopes = [new ClientScope { Scope = "api.readwrite" }]
            };

            var handler = new TokenRequestHandler(
                new FakeAuthDbContext([client], [user]),
                CreateGrantHandlers(user));

            var request = new OpenIddictRequest
            {
                ClientId = Constants.ClientId_Web,
                GrantType = GrantTypes.Password,
                Username = user.Email,
                Password = Constants.NoPassword,
                Scope = "openid"
            };

            var result = await handler.HandleAsync(request);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(Errors.InvalidScope, result.Error);
        }

        private static IHttpContextAccessor CreateHttpContextAccessor()
        {
            var context = new DefaultHttpContext();
            context.Request.Headers["X-Forwarded-For"] = new StringValues("127.0.0.1");
            return new HttpContextAccessor { HttpContext = context };
        }

        private static IReadOnlyList<ITokenGrantHandler> CreateGrantHandlers(User user)
        {
            var contextAccessor = CreateHttpContextAccessor();
            var auditLoginService = new FakeAuditLoginService();
            return
            [
                new PasswordTokenGrantHandler(new FakeUserService(user), auditLoginService, contextAccessor),
                new VerificationCodeTokenGrantHandler(new FakeVerificationCodeService(), auditLoginService, contextAccessor),
                new SingleSignOnTokenGrantHandler(new FakeSingleSignOnService(), auditLoginService, contextAccessor),
                new IntegrationTokenGrantHandler()
            ];
        }

        private sealed class FakeAuthDbContext(List<Client> clients, List<User> users) : IAuthDbContext
        {
            public IQueryable<Client> Clients => clients.AsQueryable();
            public IQueryable<User> Users => users.AsQueryable();
            public IQueryable<AuditLogin> AuditLogins => throw new NotSupportedException();

            public Task AddClient(Client entity) => throw new NotSupportedException();
            public Task AddUser(User entity) => throw new NotSupportedException();
            public Task UpdateUser(User entity) => throw new NotSupportedException();
            public Task AddAuditLogin(AuditLogin entity) => Task.CompletedTask;
            public void Dispose() { }
        }

        private sealed class FakeUserService(User user) : IUserService
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
            public Task AddAuditLogin(AuditLogin entity) => Task.CompletedTask;
        }
    }
}
