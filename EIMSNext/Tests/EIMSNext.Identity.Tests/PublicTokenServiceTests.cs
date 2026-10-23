using EIMSNext.ApiService;
using EIMSNext.Entities;
using EIMSNext.Identity.Interfaces;
using EIMSNext.Identity.Models;
using EIMSNext.Identity.Services;
using Microsoft.Extensions.Caching.Memory;

namespace EIMSNext.Identity.Tests;

[TestClass]
public sealed class PublicTokenServiceTests
{
    private const string TargetId = "form-expired";
    private const string SecretKey = "test-public-secret";

    [TestMethod]
    public void Validate_ExpiredSection_ReturnsInvalidGrantWithExpiredDescription()
    {
        var setting = new PublicAccessSetting
        {
            TargetId = TargetId,
            CorpId = "test-corp",
            Form = new PublicFormAccessSetting
            {
                FormLink = new PublishSection
                {
                    Enabled = true,
                    ExpireTime = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeMilliseconds(),
                    AccessCodeEnabled = true,
                    AccessCodeHash = "not-used"
                }
            }
        };
        var service = CreateService(setting);

        var result = service.Validate($"public_{TargetId}", "any-code", PublicScope.FormLink);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual("invalid_grant", result.Error);
        Assert.AreEqual("公开访问链接已过期", result.ErrorDescription);
    }

    [TestMethod]
    public void Validate_MissingSetting_ReturnsGenericInvalidCredential()
    {
        var service = CreateService();

        var result = service.Validate("public_missing", "any-code", PublicScope.FormLink);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual("invalid_grant", result.Error);
        Assert.AreEqual("公开访问凭证无效", result.ErrorDescription);
    }

    private static PublicTokenService CreateService(params PublicAccessSetting[] settings)
    {
        var clients = new[] { new Client { Id = InternalClients.PublicClientId, ClientSecret = SecretKey } };
        var db = new FakeIdentityDbContext(settings, clients);
        return new PublicTokenService(db, new PublicAccessKeyProvider(db, new MemoryCache(new MemoryCacheOptions())));
    }

    private sealed class FakeIdentityDbContext : IIdentityDbContext
    {
        private readonly IQueryable<PublicAccessSetting> _publicSettings;
        private readonly IQueryable<Client> _clients;

        public FakeIdentityDbContext(IEnumerable<PublicAccessSetting> settings, IEnumerable<Client>? clients = null)
        {
            _publicSettings = settings.AsQueryable();
            _clients = (clients ?? Enumerable.Empty<Client>()).AsQueryable();
        }

        public IQueryable<Client> Clients => _clients;
        public IQueryable<User> Users => Enumerable.Empty<User>().AsQueryable();
        public IQueryable<UserCorp> UserCorps => Enumerable.Empty<UserCorp>().AsQueryable();
        public IQueryable<EmployeeLookup> Employees => Enumerable.Empty<EmployeeLookup>().AsQueryable();
        public IQueryable<PublicAccessSetting> PublicSettings => _publicSettings;
        public IQueryable<CorporateSettingReadModel> CorporateSettings => Enumerable.Empty<CorporateSettingReadModel>().AsQueryable();

        public IQueryable<IntegrationLoginSetting> IntegrationLoginSettings => throw new NotImplementedException();

        public IQueryable<UserIntegrationBinding> UserIntegrationBindings => throw new NotImplementedException();

        public Task AddClient(Client entity) => Task.CompletedTask;
        public Task UpdateClient(Client entity) => Task.CompletedTask;
        public Task AddUser(User entity) => Task.CompletedTask;
        public Task UpdateUser(User entity) => Task.CompletedTask;
        public Task AddIdentityLoginAudit(IdentityLoginAudit entity) => Task.CompletedTask;
        public void Dispose() { }

        public Task AddIntegrationLoginSetting(IntegrationLoginSetting entity)
        {
            throw new NotImplementedException();
        }

        public Task UpdateIntegrationLoginSetting(IntegrationLoginSetting entity)
        {
            throw new NotImplementedException();
        }

        public Task AddUserIntegrationBinding(UserIntegrationBinding entity)
        {
            throw new NotImplementedException();
        }

        public Task UpdateUserIntegrationBinding(UserIntegrationBinding entity)
        {
            throw new NotImplementedException();
        }
    }
}
