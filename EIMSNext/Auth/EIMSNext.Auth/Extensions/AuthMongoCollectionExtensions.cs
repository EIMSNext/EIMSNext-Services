using EIMSNext.Auth.Models;
using EIMSNext.MongoDb;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace EIMSNext.Auth.Extensions
{
    public static class AuthMongoCollectionExtensions
    {
        private const string PublicSettingCollectionName = "PublicSetting";
        private const string FormDefCollectionName = "FormDef";
        private const string DashboardDefCollectionName = "DashboardDef";

        public static IServiceCollection AddAuthMongoCollections(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<MongoDbConfiguration>(configuration.GetSection("MongoDb"));

            services.AddSingleton<IMongoClient>(sp =>
            {
                var settings = sp.GetRequiredService<IOptions<MongoDbConfiguration>>().Value;
                if (string.IsNullOrWhiteSpace(settings.ConnectionString))
                {
                    throw new InvalidOperationException("MongoDb:ConnectionString 未配置");
                }
                return new MongoClient(settings.ConnectionString);
            });

            services.AddSingleton<IMongoDatabase>(sp =>
            {
                var client = sp.GetRequiredService<IMongoClient>();
                var settings = sp.GetRequiredService<IOptions<MongoDbConfiguration>>().Value;
                if (string.IsNullOrWhiteSpace(settings.Database))
                {
                    throw new InvalidOperationException("MongoDb:Database 未配置");
                }
                return client.GetDatabase(settings.Database);
            });

            services.AddSingleton<IMongoCollection<PublicAccessSetting>>(sp =>
            {
                var db = sp.GetRequiredService<IMongoDatabase>();
                return db.GetCollection<PublicAccessSetting>(PublicSettingCollectionName);
            });

            return services;
        }
    }
}
