using EIMSNext.Auth.Entities;
using EIMSNext.MongoDb;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace EIMSNext.Auth.DbMaintenance
{
    public class EIMSDbContext : MongoDbContextBase
    {
        public EIMSDbContext(IOptions<MongoDbConfiguration> settings) : base(settings)
        {
        }

        public IMongoCollection<Client> Clients => Database.GetCollection<Client>(nameof(Client));
        public IMongoCollection<User> Users => Database.GetCollection<User>(nameof(User));
    }
}
