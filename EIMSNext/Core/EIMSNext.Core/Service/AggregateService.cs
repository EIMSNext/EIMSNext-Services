using EIMSNext.MongoDb;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EIMSNext.Core.Service
{
    public class AggregateService
    {
        public AggregateService(IMongoDbContex dbContext)
        {
            DbContext = dbContext;
        }

        private IMongoDbContex DbContext { get; set; }

        public Task<IAsyncCursor<BsonDocument>> Calucate(PipelineDefinition<BsonDocument, BsonDocument> pipeline, AggregateOptions? options = null)
        {
            return DbContext.Database.GetCollection<BsonDocument>("FormData").AggregateAsync(pipeline, options);
        }
    }
}
