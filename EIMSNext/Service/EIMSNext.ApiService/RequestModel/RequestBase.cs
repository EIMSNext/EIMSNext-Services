using EIMSNext.Core.Entity;

namespace EIMSNext.ApiService.RequestModel
{
    public abstract class RequestBase : IMongoEntity
    {
        public string Id { get; set; } = "";
    }
}
