using EIMSNext.ApiCore;
using EIMSNext.Auth.DbMaintenance;
using EIMSNext.MongoDb;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables();

var mongoSection = builder.Configuration.GetSection("MongoDb");
var connectionString = mongoSection["ConnectionString"];
var database = mongoSection["Database"];
if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(database))
{
    throw new InvalidOperationException("缺少 MongoDb 配置，请在 appsettings.json、环境变量或命令行中提供 MongoDb:ConnectionString 和 MongoDb:Database。");
}

builder.Services.AddBasicServices(builder.Configuration);
builder.Services.AddSingleton<EIMSDbContext>();
builder.Services.AddSingleton<DbIndexManager>();

using var host = builder.Build();
var indexManager = host.Services.GetRequiredService<DbIndexManager>();
indexManager.CreateIndexes();

Console.WriteLine($"Mongo indexes created for database '{database}'.");
