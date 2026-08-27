using EIMSNext.Entities;
using EIMSNext.Identity.Interfaces;
using EIMSNext.Identity.Services;

namespace EIMSNext.Identity.Tests;

[TestClass]
public sealed class SingleSignOnServiceTests
{
    [TestMethod]
    public void Validate_UsesSecretForTheRequestedCorporateId()
    {
        var user = new User { Id = "user-1", Name = "User" };
        var service = new SingleSignOnService(
            new FakeUserService(user),
            new FakeIdentityDbContext([
                new CorporateSettingReadModel { CorpId = "corp-a", Name = CorporateSettingNames.SsoSecret, Value = "secret-a" },
                new CorporateSettingReadModel { CorpId = "corp-b", Name = CorporateSettingNames.SsoSecret, Value = "secret-b" }
            ]));

        Assert.AreSame(user, service.Validate("corp-a_emp-1", "secret-a"));
        Assert.IsNull(service.Validate("corp-a_emp-1", "secret-b"));
        Assert.IsNull(service.Validate("corp-b_emp-1", "secret-a"));
    }

    [TestMethod]
    public void Validate_RejectsMissingOrEmptyCorporateSecret()
    {
        var user = new User { Id = "user-1", Name = "User" };
        var service = new SingleSignOnService(
            new FakeUserService(user),
            new FakeIdentityDbContext([
                new CorporateSettingReadModel { CorpId = "corp-empty", Name = CorporateSettingNames.SsoSecret, Value = string.Empty }
            ]));

        Assert.IsNull(service.Validate("corp-missing_emp-1", "secret"));
        Assert.IsNull(service.Validate("corp-empty_emp-1", "secret"));
        Assert.IsNull(service.Validate("corp-empty_emp-1", string.Empty));
    }

    private sealed class FakeIdentityDbContext(
        IEnumerable<CorporateSettingReadModel> settings) : IIdentityDbContext
    {
        private readonly List<CorporateSettingReadModel> _settings = settings.ToList();

        public IQueryable<Client> Clients => Array.Empty<Client>().AsQueryable();
        public IQueryable<User> Users => Array.Empty<User>().AsQueryable();
        public IQueryable<EmployeeLookup> Employees => Array.Empty<EmployeeLookup>().AsQueryable();
        public IQueryable<EIMSNext.Identity.Models.PublicAccessSetting> PublicSettings => Array.Empty<EIMSNext.Identity.Models.PublicAccessSetting>().AsQueryable();
        public IQueryable<CorporateSettingReadModel> CorporateSettings => _settings.AsQueryable();

        public Task AddClient(Client entity) => Task.CompletedTask;
        public Task UpdateClient(Client entity) => Task.CompletedTask;
        public Task AddUser(User entity) => Task.CompletedTask;
        public Task UpdateUser(User entity) => Task.CompletedTask;
        public Task AddIdentityLoginAudit(IdentityLoginAudit entity) => Task.CompletedTask;

        public void Dispose()
        {
        }
    }

    private sealed class FakeUserService(User user) : IUserService
    {
        public User? Validate(string emailOrPhone, string password) => null;
        public User? FindById(string id) => null;
        public User? FindByEmailOrPhone(string emailOrPhone) => null;
        public User? FindByEmail(string email) => null;
        public User? FindByPhone(string phone) => null;
        public User? FindByEmpNo(string corpId, string empNo) => corpId == "corp-a" || corpId == "corp-b" ? user : null;
        public Client? FindEnabledClient(string clientId) => null;
        public bool VerifyPassword(User user, string password) => false;
    }
}
