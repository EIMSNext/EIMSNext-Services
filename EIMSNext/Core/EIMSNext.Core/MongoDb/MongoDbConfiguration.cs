using MongoDB.Driver;

namespace EIMSNext.MongoDb
{
    public class MongoDbConfiguration
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string Database { get; set; } = string.Empty;
        public SslSettings? SslSettings { get; set; }
    }
}
