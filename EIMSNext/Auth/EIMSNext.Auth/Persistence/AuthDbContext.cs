using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Interfaces;
using EIMSNext.MongoDb;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace EIMSNext.Auth.Persistence
{
    public class AuthDbContext : MongoDbContextBase, IAuthDbContext
    {
        private readonly IMongoCollection<Client> _clients;
        private readonly IMongoCollection<User> _users;
        private readonly IMongoCollection<AuditLogin> _auditLogin;
        private readonly IMongoCollection<IntegrationLoginSetting> _integrationLoginSettings;
        private readonly IMongoCollection<UserIntegrationBinding> _userIntegrationBindings;

        public AuthDbContext(IOptions<MongoDbConfiguration> settings)
            : base(settings)
        {
            _clients = Database.GetCollection<Client>(nameof(Client));
            _users = Database.GetCollection<User>(nameof(User));
            _auditLogin = Database.GetCollection<AuditLogin>(nameof(AuditLogin));
            _integrationLoginSettings = Database.GetCollection<IntegrationLoginSetting>(nameof(IntegrationLoginSetting));
            _userIntegrationBindings = Database.GetCollection<UserIntegrationBinding>(nameof(UserIntegrationBinding));
        }

        #region IConfigurationDbContext

        public IQueryable<Client> Clients => _clients.AsQueryable();
        public IQueryable<User> Users => _users.AsQueryable();
        public IQueryable<IntegrationLoginSetting> IntegrationLoginSettings => _integrationLoginSettings.AsQueryable();
        public IQueryable<UserIntegrationBinding> UserIntegrationBindings => _userIntegrationBindings.AsQueryable();

        public async Task AddClient(Client entity)
        {
            await _clients.InsertOneAsync(entity);
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

        public async Task AddAuditLogin(AuditLogin entity)
        {
            await this._auditLogin.InsertOneAsync(entity);
        }

        #endregion
    }
}
