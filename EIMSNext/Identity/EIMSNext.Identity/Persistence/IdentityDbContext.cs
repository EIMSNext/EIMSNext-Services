using EIMSNext.Entities;
using EIMSNext.Identity.Interfaces;
using EIMSNext.Identity.Models;
using EIMSNext.Core.Mongo;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace EIMSNext.Identity.Persistence
{
    public class IdentityDbContext : MongoDbContextBase, IIdentityDbContext
    {
        private readonly IMongoCollection<Client> _clients;
        private readonly IMongoCollection<User> _users;
        private readonly IMongoCollection<EmployeeLookup> _employees;
        private readonly IMongoCollection<IntegrationLoginSetting> _integrationLoginSettings;
        private readonly IMongoCollection<UserIntegrationBinding> _userIntegrationBindings;
        private readonly IMongoCollection<IdentityLoginAudit> _auditLogin;
        private readonly IMongoCollection<PublicAccessSetting> _publicSettings;
        private readonly IMongoCollection<CorporateSettingReadModel> _corporateSettings;

        public IdentityDbContext(IOptions<MongoDbConfiguration> settings)
            : base(settings)
        {
            _clients = Database.GetCollection<Client>(nameof(Client));
            _users = Database.GetCollection<User>(nameof(User));
            _employees = Database.GetCollection<EmployeeLookup>("Employee");
            _integrationLoginSettings = Database.GetCollection<IntegrationLoginSetting>(nameof(IntegrationLoginSetting));
            _userIntegrationBindings = Database.GetCollection<UserIntegrationBinding>(nameof(UserIntegrationBinding));
            _auditLogin = Database.GetCollection<IdentityLoginAudit>(nameof(IdentityLoginAudit));
            _publicSettings = Database.GetCollection<PublicAccessSetting>("PublicSetting");
            _corporateSettings = Database.GetCollection<CorporateSettingReadModel>("CorporateSetting");
        }

        #region IConfigurationDbContext

        public IQueryable<Client> Clients => _clients.AsQueryable();
        public IQueryable<User> Users => _users.AsQueryable();
        public IQueryable<IntegrationLoginSetting> IntegrationLoginSettings => _integrationLoginSettings.AsQueryable();
        public IQueryable<UserIntegrationBinding> UserIntegrationBindings => _userIntegrationBindings.AsQueryable();
        public IQueryable<EmployeeLookup> Employees => _employees.AsQueryable();
        public IQueryable<PublicAccessSetting> PublicSettings => _publicSettings.AsQueryable();
        public IQueryable<CorporateSettingReadModel> CorporateSettings => _corporateSettings.AsQueryable();

        public async Task AddClient(Client entity)
        {
            await _clients.InsertOneAsync(entity);
        }

        public Task UpdateClient(Client entity)
        {
            return _clients.ReplaceOneAsync(x => x.Id == entity.Id, entity);
        }

        public async Task AddUser(User entity)
        {
            await this._users.InsertOneAsync(entity);
        }

        public Task UpdateUser(User entity)
        {
            return _users.ReplaceOneAsync(x => x.Id == entity.Id, entity);
        }

        public Task AddIntegrationLoginSetting(IntegrationLoginSetting entity)
        {
            return _integrationLoginSettings.InsertOneAsync(entity);
        }

        public Task UpdateIntegrationLoginSetting(IntegrationLoginSetting entity)
        {
            return _integrationLoginSettings.ReplaceOneAsync(x => x.Id == entity.Id, entity);
        }

        public Task AddUserIntegrationBinding(UserIntegrationBinding entity)
        {
            return _userIntegrationBindings.InsertOneAsync(entity);
        }

        public Task UpdateUserIntegrationBinding(UserIntegrationBinding entity)
        {
            return _userIntegrationBindings.ReplaceOneAsync(x => x.Id == entity.Id, entity);
        }

        public async Task AddIdentityLoginAudit(IdentityLoginAudit entity)
        {
            await this._auditLogin.InsertOneAsync(entity);
        }

        public Task AddIdentityLoginAudits(IReadOnlyCollection<IdentityLoginAudit> entities, CancellationToken cancellationToken = default)
        {
            if (entities.Count == 0)
            {
                return Task.CompletedTask;
            }

            var writes = entities
                .Select(entity => new ReplaceOneModel<IdentityLoginAudit>(
                    Builders<IdentityLoginAudit>.Filter.Eq(x => x.Id, entity.Id),
                    entity)
                {
                    IsUpsert = true
                })
                .Cast<WriteModel<IdentityLoginAudit>>()
                .ToList();

            return _auditLogin.BulkWriteAsync(
                writes,
                new BulkWriteOptions { IsOrdered = false },
                cancellationToken);
        }

        #endregion
    }
}
