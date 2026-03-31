using EIMSNext.MongoDb;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EIMSNext.Core.Services
{
    public class AggregateService
    {
        public AggregateService(IMongoDbContex dbContext)
        {
            DbContext = dbContext;
        }

        private IMongoDbContex DbContext { get; set; }

        public IMongoCollection<BsonDocument> GetCollection(string name)
        {
            return DbContext.Database.GetCollection<BsonDocument>("FormData");
        }
    }
}
