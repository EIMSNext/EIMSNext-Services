using EIMSNext.Core.Entity;
using EIMSNext.Entity;
using EIMSNext.MongoDb;

using Microsoft.Extensions.Options;

using MongoDB.Driver;

namespace EIMSNext.Repository
{
    public class EIMSDbContext : MongoDbContextBase
    {
        #region Variables

        #endregion

        public EIMSDbContext(IOptions<MongoDbConfiguration> settings) : base(settings)
        {
            CreateIndexes();
        }

        #region Properties

        #endregion

        #region Methods


        #endregion

        #region Helper

        private void CreateIndexes()
        {
            var indexOptions = new CreateIndexOptions() { Background = true };
            CreateCorpIdIndexes<Department>(indexOptions);
            CreateCorpIdIndexes<Employee>(indexOptions);
            CreateCorpIdIndexes<Role>(indexOptions);
        }

        private void CreateCorpIdIndexes<T>(CreateIndexOptions indexOptions) where T : CorpEntityBase
        {
            var collection = Database.GetCollection<T>(typeof(T).Name);
            var builder = Builders<T>.IndexKeys;
            var corpIdIndexModel = new CreateIndexModel<T>(builder.Ascending(_ => _.CorpId), indexOptions);
            collection.Indexes.CreateOne(corpIdIndexModel);
        }

        #endregion
    }
}