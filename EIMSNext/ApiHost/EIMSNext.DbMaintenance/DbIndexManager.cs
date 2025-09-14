using EIMSNext.Auth.Entity;
using EIMSNext.Core.Entity;
using EIMSNext.Entity;
using MongoDB.Driver;

namespace EIMSNext.Auth.DbMaintenance
{
    public class DbIndexManager
    {
        private EIMSDbContext _dbContext;
        public DbIndexManager(EIMSDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public void CreateIndexes()
        {
            var indexOptions = new CreateIndexOptions() { Background = true };

            CreatePersistedGrantsIndexes(indexOptions);
            CreateClientsIndexes(indexOptions);
            CreateIdentityResourcesIndexes(indexOptions);
            CreateApiResourcesIndexes(indexOptions);
            CreateApiScopesIndexes(indexOptions);
            CreateUsersIndexes(indexOptions);

            CreateCorpIdIndexes<Department>(indexOptions);
            CreateCorpIdIndexes<Employee>(indexOptions);
            CreateCorpIdIndexes<Role>(indexOptions);
        }
        private void CreatePersistedGrantsIndexes(CreateIndexOptions indexOptions)
        {
            var builder = Builders<PersistedGrant>.IndexKeys;

            var subIndexModel = new CreateIndexModel<PersistedGrant>(builder.Ascending(_ => _.UserId), indexOptions);
            var clientIdSubIndexModel = new CreateIndexModel<PersistedGrant>(
              builder.Combine(
                  builder.Ascending(_ => _.ClientId),
                  builder.Ascending(_ => _.UserId)),
              indexOptions);

            var clientIdSubTypeIndexModel = new CreateIndexModel<PersistedGrant>(
              builder.Combine(
                  builder.Ascending(_ => _.ClientId),
                  builder.Ascending(_ => _.UserId),
                  builder.Ascending(_ => _.Type)),
              indexOptions);

            _dbContext.PersistedGrants.Indexes.CreateOne(subIndexModel);
            _dbContext.PersistedGrants.Indexes.CreateOne(clientIdSubIndexModel);
            _dbContext.PersistedGrants.Indexes.CreateOne(clientIdSubTypeIndexModel);
        }

        private void CreateClientsIndexes(CreateIndexOptions indexOptions)
        {
            var builder = Builders<Client>.IndexKeys;
            var clientIdIndexModel = new CreateIndexModel<Client>(builder.Ascending(_ => _.Id), indexOptions);
            _dbContext.Clients.Indexes.CreateOne(clientIdIndexModel);
        }

        private void CreateIdentityResourcesIndexes(CreateIndexOptions indexOptions)
        {
            var builder = Builders<IdentityResource>.IndexKeys;
            var nameIndexModel = new CreateIndexModel<IdentityResource>(builder.Ascending(_ => _.Name), indexOptions);
            _dbContext.IdentityResources.Indexes.CreateOne(nameIndexModel);
        }

        private void CreateApiResourcesIndexes(CreateIndexOptions indexOptions)
        {
            var builder = Builders<ApiResource>.IndexKeys;
            var nameIndexModel = new CreateIndexModel<ApiResource>(builder.Ascending(_ => _.Name), indexOptions);
            var scopesIndexModel = new CreateIndexModel<ApiResource>(builder.Ascending(_ => _.Scopes), indexOptions);
            _dbContext.ApiResources.Indexes.CreateOne(nameIndexModel);
            _dbContext.ApiResources.Indexes.CreateOne(scopesIndexModel);
        }

        private void CreateApiScopesIndexes(CreateIndexOptions indexOptions)
        {
            var builder = Builders<ApiScope>.IndexKeys;
            var nameIndexModel = new CreateIndexModel<ApiScope>(builder.Ascending(_ => _.Name), indexOptions);
            _dbContext.ApiScopes.Indexes.CreateOne(nameIndexModel);
        }

        private void CreateUsersIndexes(CreateIndexOptions indexOptions)
        {
            var builder = Builders<User>.IndexKeys;
            var emailIndexModel = new CreateIndexModel<User>(builder.Ascending(_ => _.Email), indexOptions);
            var phoneIndexModel = new CreateIndexModel<User>(builder.Ascending(_ => _.Phone), indexOptions);
            _dbContext.Users.Indexes.CreateOne(emailIndexModel);
            _dbContext.Users.Indexes.CreateOne(phoneIndexModel);
        }
         
        private void CreateCorpIdIndexes<T>(CreateIndexOptions indexOptions) where T : CorpEntityBase
        {
            var collection = _dbContext.Database.GetCollection<T>(typeof(T).Name);
            var builder = Builders<T>.IndexKeys;
            var corpIdIndexModel = new CreateIndexModel<T>(builder.Ascending(_ => _.CorpId), indexOptions);
            collection.Indexes.CreateOne(corpIdIndexModel);
        }
    }
}
