using EIMSNext.Core.Entities;

namespace EIMSNext.ApiService.RequestModels
{
    public abstract class RequestBase : IMongoEntity
    {
        public string Id { get; set; } = "";
    }
}
