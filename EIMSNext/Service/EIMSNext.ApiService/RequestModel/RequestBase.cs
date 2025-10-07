using System.Text.Json.Serialization;
using EIMSNext.Common;
using EIMSNext.Core.Entity;

namespace EIMSNext.ApiService.RequestModel
{
    public abstract class RequestBase : IMongoEntity
    {
        [JsonPropertyName(Constants.Field_BsonId)]
        public string Id { get; set; } = "";
    }
}
