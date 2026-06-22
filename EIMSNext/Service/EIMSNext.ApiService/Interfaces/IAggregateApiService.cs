using EIMSNext.ApiService.RequestModels;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EIMSNext.ApiService
{
    public interface IAggregateApiService: IApiService
    {
        Task<IAsyncCursor<BsonDocument>?> Calucate(AggCalcRequest request);
        Task<IAsyncCursor<BsonDocument>?> Calucate(AggCalcRequest request, string corpId);
        Task<long> Count(AggCalcRequest request);
        Task<long> Count(AggCalcRequest request, string corpId);
    }
}
