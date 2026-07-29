using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;

namespace EIMSNext.ApiService.RequestModels
{
    public abstract class RequestBase : IMongoEntity
    {
        public string Id { get; set; } = "";
    }
}
