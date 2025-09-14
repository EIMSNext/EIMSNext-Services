using EIMSNext.Auth.Entity;
using EIMSNext.Auth.Interfaces;
using EIMSNext.MongoDb;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace EIMSNext.Auth.DbContext
{
    public class AuthDbContext : MongoDbContextBase, IAuthDbContext
    {
        private readonly IMongoCollection<Client> _clients;
        private readonly IMongoCollection<IdentityResource> _identityResources;
        private readonly IMongoCollection<ApiResource> _apiResources;
        private readonly IMongoCollection<ApiScope> _apiScopes;
        private readonly IMongoCollection<User> _users;
        private readonly IMongoCollection<PersistedGrant> _persistedGrants;

        public AuthDbContext(IOptions<MongoDbConfiguration> settings)
            : base(settings)
        {
            _clients = Database.GetCollection<Client>(nameof(Client));
            _identityResources = Database.GetCollection<IdentityResource>(nameof(IdentityResource));
            _apiResources = Database.GetCollection<ApiResource>(nameof(ApiResource));
            _apiScopes = Database.GetCollection<ApiScope>(nameof(ApiScope));
            _users = Database.GetCollection<User>(nameof(User));
            _persistedGrants = Database.GetCollection<PersistedGrant>(nameof(PersistedGrant));
        }

        #region IConfigurationDbContext

        public IQueryable<Client> Clients => _clients.AsQueryable();

        public IQueryable<IdentityResource> IdentityResources => _identityResources.AsQueryable();

        public IQueryable<ApiResource> ApiResources => _apiResources.AsQueryable();

        public IQueryable<ApiScope> ApiScopes => _apiScopes.AsQueryable();
        public IQueryable<User> Users => _users.AsQueryable();

        public async Task AddClient(Client entity)
        {
            await _clients.InsertOneAsync(entity);
        }

        public async Task AddIdentityResource(IdentityResource entity)
        {
            await _identityResources.InsertOneAsync(entity);
        }

        public async Task AddApiResource(ApiResource entity)
        {
            await _apiResources.InsertOneAsync(entity);
        }

        public async Task AddApiScope(ApiScope entity)
        {
            await this._apiScopes.InsertOneAsync(entity);
        }

        public async Task AddUser(User entity)
        {
            await this._users.InsertOneAsync(entity);
        }

        #endregion

        #region IPersistedGrantDbContext

        public IQueryable<PersistedGrant> PersistedGrants
        {
            get { return _persistedGrants.AsQueryable(); }
        }

        public Task RemovePersistedGrant(Expression<Func<PersistedGrant, bool>> filter)
        {
            return _persistedGrants.DeleteManyAsync(filter);
        }

        public Task RemoveExpiredPersistedGrant()
        {
            return RemovePersistedGrant(x => x.Expiration < DateTime.UtcNow);
        }

        public Task InsertOrUpdatePersistedGrant(Expression<Func<PersistedGrant, bool>> filter, PersistedGrant entity)
        {
            return _persistedGrants.ReplaceOneAsync(filter, entity, new ReplaceOptions() { IsUpsert = true });
        }
        #endregion
    }
}
