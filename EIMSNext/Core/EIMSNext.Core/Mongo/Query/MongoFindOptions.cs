using MongoDB.Driver;
using EIMSNext.Core.Query;

namespace EIMSNext.Core.Mongo.Query
{
    public class MongoFindOptions<T>
    {
        public MongoFindOptions()
        {
            Filter = Builders<T>.Filter.Empty;
        }

        public FilterDefinition<T> Filter { get; set; }
        public SortDefinition<T>? Sort { get; set; }
        //public ProjectionDefinition<T>? Projection { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; } = 20;

        public FindOptions? Options { get; set; }

        public int GetEffectiveTake()
        {
            return Take <= 0 ? DynamicFindOptions<T>.DefaultTakeWhenUnspecified : Take;
        }

        public int GetEffectiveSkip()
        {
            return Math.Max(0, Skip);
        }
    }
}
