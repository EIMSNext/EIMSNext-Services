using EIMSNext.MongoDb;

namespace EIMSNext.Core.Tests
{
    public class DbContext : MongoDbContextBase
    {
        //public DbContext(IOptions<MongoDbConfiguration> settings) : base(settings)
        //{
        //}
        public DbContext(MongoDbConfiguration setting) : base(setting)
        {
        }

        public static DbContext Create()
        {
            return new DbContext(new MongoDbConfiguration() { ConnectionString = "mongodb://localhost:27017", Database = "EIMSTest" });
        }
    }
}
