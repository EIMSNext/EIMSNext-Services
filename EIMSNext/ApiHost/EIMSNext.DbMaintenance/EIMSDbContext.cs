using EIMSNext.Auth.Entity;
using EIMSNext.MongoDb;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace EIMSNext.Auth.DbMaintenance
{
    public class EIMSDbContext : MongoDbContextBase
    {
        public EIMSDbContext(IOptions<MongoDbConfiguration> settings) : base(settings)
        {
        }

        public IMongoCollection<Client> Clients => Database.GetCollection<Client>(nameof(Client));
        public IMongoCollection<IdentityResource> IdentityResources => Database.GetCollection<IdentityResource>(nameof(IdentityResource));
        public IMongoCollection<ApiResource> ApiResources => Database.GetCollection<ApiResource>(nameof(ApiResource));
        public IMongoCollection<ApiScope> ApiScopes => Database.GetCollection<ApiScope>(nameof(ApiScope));
        public IMongoCollection<User> Users => Database.GetCollection<User>(nameof(User));
        public IMongoCollection<PersistedGrant> PersistedGrants => Database.GetCollection<PersistedGrant>(nameof(PersistedGrant));
    }
}
