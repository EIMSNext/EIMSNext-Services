using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Interfaces;
using EIMSNext.Auth.Models;
using EIMSNext.MongoDb;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace EIMSNext.Auth.Persistence
{
    public class AuthDbContext : MongoDbContextBase, IAuthDbContext
    {
        private readonly IMongoCollection<Client> _clients;
        private readonly IMongoCollection<User> _users;
        private readonly IMongoCollection<EmployeeLookup> _employees;
        private readonly IMongoCollection<AuditLogin> _auditLogin;
        private readonly IMongoCollection<PublicAccessSetting> _publicSettings;
        private readonly IMongoCollection<CorporateSettingReadModel> _corporateSettings;

        public AuthDbContext(IOptions<MongoDbConfiguration> settings)
            : base(settings)
        {
            _clients = Database.GetCollection<Client>(nameof(Client));
            _users = Database.GetCollection<User>(nameof(User));
            _employees = Database.GetCollection<EmployeeLookup>("Employee");
            _auditLogin = Database.GetCollection<AuditLogin>(nameof(AuditLogin));
            _publicSettings = Database.GetCollection<PublicAccessSetting>("PublicSetting");
            _corporateSettings = Database.GetCollection<CorporateSettingReadModel>("CorporateSetting");
        }

        #region IConfigurationDbContext

        public IQueryable<Client> Clients => _clients.AsQueryable();
        public IQueryable<User> Users => _users.AsQueryable();
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

        public async Task AddAuditLogin(AuditLogin entity)
        {
            await this._auditLogin.InsertOneAsync(entity);
        }

        #endregion
    }
}
