using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace EIMSNext.MongoDb
{
    public interface IMongoDbContex : IDisposable
    {
        IMongoDatabase Database { get; }
    }

    public abstract class MongoDbContextBase : IMongoDbContex, IDisposable
    {
        private readonly IMongoClient _client;

        protected MongoDbContextBase(IOptions<MongoDbConfiguration> settings)
            : this(settings.Value)
        {
        }
        protected MongoDbContextBase(MongoDbConfiguration setting)
        {
            if (setting.ConnectionString == null)
                throw new ArgumentNullException(nameof(setting), "MongoDbConfiguration.ConnectionString cannot be null.");

            var mongoUrl = MongoUrl.Create(setting.ConnectionString);

            if (setting.Database == null && mongoUrl.DatabaseName == null)
                throw new ArgumentNullException(nameof(setting), "MongoDbConfiguration.Database cannot be null.");

            var clientSettings = MongoClientSettings.FromUrl(mongoUrl);

            if (setting.SslSettings != null)
            {
                clientSettings.SslSettings = setting.SslSettings;
                clientSettings.UseTls = true;
            }

            _client = new MongoClient(clientSettings);
            Database = _client.GetDatabase(setting.Database ?? mongoUrl.DatabaseName);
        }

        public IMongoDatabase Database { get; private set; }

        public void Dispose()
        {
            // TODO
        }
    }
}
