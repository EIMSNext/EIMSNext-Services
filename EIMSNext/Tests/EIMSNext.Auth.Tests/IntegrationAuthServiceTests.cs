using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Integrations.Abstractions;
using EIMSNext.Auth.Interfaces;
using EIMSNext.Auth.Services;

namespace EIMSNext.Auth.Tests
{
    [TestClass]
    public class IntegrationAuthServiceTests
    {
        [TestMethod]
        public async Task ValidateAsync_AutoProvisionsUser_WhenProviderAllowsIt()
        {
            var dbContext = new FakeAuthDbContext(
                settings:
                [
                    new IntegrationLoginSetting
                    {
                        Id = IntegrationLoginType.WeChat,
                        Type = IntegrationLoginType.WeChat,
                        Enabled = true,
                        DisplayName = "微信"
                    }
                ]);
            var service = new IntegrationAuthService(
                dbContext,
                new FakeIntegrationProviderResolver(new FakeIntegrationProvider(
                    IntegrationLoginType.WeChat,
                    new IntegrationProviderCapability
                    {
                        CanAutoProvisionUser = true,
                        UnboundFailureMessage = "该微信账号还未绑定到用户",
                        DefaultUserName = "微信用户"
                    },
                    new IntegrationAuthResult
                    {
                        IntegrationType = IntegrationLoginType.WeChat,
                        OpenId = "openid-001",
                        DisplayName = "测试微信用户",
                        Avatar = "https://avatar.test/wechat.png"
                    })));

            var result = await service.ValidateAsync(IntegrationLoginType.WeChat, "code-001|state-001");

            Assert.IsTrue(result.Succeeded);
            Assert.IsNotNull(result.User);
            Assert.AreEqual("测试微信用户", result.User.Name);
            Assert.AreEqual(1, dbContext.AddedUsers.Count);
            Assert.AreEqual(1, dbContext.AddedBindings.Count);
            Assert.AreEqual(IntegrationLoginType.WeChat, dbContext.AddedBindings[0].IntegrationType);
            Assert.AreEqual("openid-001", dbContext.AddedBindings[0].OpenId);
        }

        [TestMethod]
        public async Task ValidateAsync_ReturnsFailure_WhenProviderMissing()
        {
            var service = new IntegrationAuthService(
                new FakeAuthDbContext(
                    settings:
                    [
                        new IntegrationLoginSetting
                        {
                            Id = IntegrationLoginType.Feishu,
                            Type = IntegrationLoginType.Feishu,
                            Enabled = true,
                            DisplayName = "飞书"
                        }
                    ]),
                new FakeIntegrationProviderResolver());

            var result = await service.ValidateAsync(IntegrationLoginType.Feishu, "code-001|state-001");

            Assert.IsFalse(result.Succeeded);
            Assert.IsNull(result.User);
            Assert.AreEqual(string.Empty, result.FailureMessage);
        }

        private sealed class FakeIntegrationProviderResolver(params IIntegrationProvider[] providers) : IIntegrationProviderResolver
        {
            private readonly Dictionary<string, IIntegrationProvider> _providers = providers.ToDictionary(x => x.Type, StringComparer.OrdinalIgnoreCase);

            public bool TryGetById(string id, out IIntegrationProvider? provider)
            {
                return _providers.TryGetValue(id, out provider);
            }
        }

        private sealed class FakeIntegrationProvider(string type, IntegrationProviderCapability capability, IntegrationAuthResult authResult) : IIntegrationProvider
        {
            public string Type => type;

            public IntegrationProviderCapability Capability => capability;

            public Task<IntegrationAuthResult> AuthenticateAsync(IntegrationLoginSetting setting, IntegrationAuthPayload payload, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(authResult);
            }

            public string BuildAuthorizationUrl(IntegrationLoginSetting setting, string state)
            {
                return $"https://example.test/{type}?state={state}";
            }
        }

        private sealed class FakeAuthDbContext(
            IEnumerable<Client>? clients = null,
            IEnumerable<User>? users = null,
            IEnumerable<IntegrationLoginSetting>? settings = null,
            IEnumerable<UserIntegrationBinding>? bindings = null) : IAuthDbContext
        {
            private readonly List<Client> _clients = clients?.ToList() ?? [];
            private readonly List<User> _users = users?.ToList() ?? [];
            private readonly List<IntegrationLoginSetting> _settings = settings?.ToList() ?? [];
            private readonly List<UserIntegrationBinding> _bindings = bindings?.ToList() ?? [];

            public List<User> AddedUsers { get; } = [];

            public List<UserIntegrationBinding> AddedBindings { get; } = [];

            public IQueryable<Client> Clients => _clients.AsQueryable();

            public IQueryable<User> Users => _users.AsQueryable();

            public IQueryable<IntegrationLoginSetting> IntegrationLoginSettings => _settings.AsQueryable();

            public IQueryable<UserIntegrationBinding> UserIntegrationBindings => _bindings.AsQueryable();

            public Task AddClient(Client entity)
            {
                _clients.Add(entity);
                return Task.CompletedTask;
            }

            public Task AddUser(User entity)
            {
                entity.Id = string.IsNullOrWhiteSpace(entity.Id) ? $"user-{AddedUsers.Count + 1}" : entity.Id;
                _users.Add(entity);
                AddedUsers.Add(entity);
                return Task.CompletedTask;
            }

            public Task UpdateUser(User entity) => Task.CompletedTask;

            public Task AddIntegrationLoginSetting(IntegrationLoginSetting entity)
            {
                _settings.Add(entity);
                return Task.CompletedTask;
            }

            public Task UpdateIntegrationLoginSetting(IntegrationLoginSetting entity) => Task.CompletedTask;

            public Task AddUserIntegrationBinding(UserIntegrationBinding entity)
            {
                _bindings.Add(entity);
                AddedBindings.Add(entity);
                return Task.CompletedTask;
            }

            public Task UpdateUserIntegrationBinding(UserIntegrationBinding entity) => Task.CompletedTask;

            public Task AddAuditLogin(AuditLogin entity) => Task.CompletedTask;

            public void Dispose()
            {
            }
        }
    }
}
