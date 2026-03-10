using EIMSNext.ApiService.RequestModel;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EIMSNext.ApiService
{
    public interface IAggregateApiService: IApiService
    {
        Task<IAsyncCursor<BsonDocument>?> Calucate(AggCalcRequest request);
    }
}
