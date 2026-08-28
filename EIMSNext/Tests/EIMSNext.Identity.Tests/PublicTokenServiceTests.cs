using EIMSNext.ApiService;
using EIMSNext.Entities;
using EIMSNext.Identity.Interfaces;
using EIMSNext.Identity.Models;
using EIMSNext.Identity.Services;
using Microsoft.Extensions.Options;

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
        return new PublicTokenService(
            new FakeIdentityDbContext(settings),
            Options.Create(new PublicAccessOptions { SecretKey = SecretKey }));
    }

    private sealed class FakeIdentityDbContext : IIdentityDbContext
    {
        private readonly IQueryable<PublicAccessSetting> _publicSettings;

        public FakeIdentityDbContext(IEnumerable<PublicAccessSetting> settings)
        {
            _publicSettings = settings.AsQueryable();
        }

        public IQueryable<Client> Clients => Enumerable.Empty<Client>().AsQueryable();
        public IQueryable<User> Users => Enumerable.Empty<User>().AsQueryable();
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
