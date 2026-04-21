using EIMSNext.Auth.Entities;
using EIMSNext.Core.Entities;
using EIMSNext.Service.Entities;
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

            CreateClientsIndexes(indexOptions);
            CreateUsersIndexes(indexOptions);

            CreateCorpIdIndexes<Department>(indexOptions);
            CreateCorpIdIndexes<Employee>(indexOptions);
            CreateCorpIdIndexes<Role>(indexOptions);
        }
        private void CreateClientsIndexes(CreateIndexOptions indexOptions)
        {
            var builder = Builders<Client>.IndexKeys;
            var clientIdIndexModel = new CreateIndexModel<Client>(builder.Ascending(_ => _.Id), indexOptions);
            _dbContext.Clients.Indexes.CreateOne(clientIdIndexModel);
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
