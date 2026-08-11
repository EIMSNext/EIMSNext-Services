using MongoDB.Bson;
using MongoDB.Driver;

namespace EIMSNext.Async.Host;

internal sealed class QuartzMongoIndexInitializer(IConfiguration configuration)
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var quartzConfiguration = configuration.GetSection("Quartz");
        var connectionString = quartzConfiguration.GetValue<string>("ConnectionString")
            ?? throw new InvalidOperationException("Missing Quartz MongoDB connection string");
        var collectionPrefix = quartzConfiguration.GetValue<string>("CollectionPrefix") ?? "quartz";
        var mongoUrl = new MongoUrl(connectionString);
        if (string.IsNullOrWhiteSpace(mongoUrl.DatabaseName))
        {
            throw new InvalidOperationException("Quartz MongoDB connection string must include a database name.");
        }

        var database = new MongoClient(mongoUrl).GetDatabase(mongoUrl.DatabaseName);
        var triggers = database.GetCollection<BsonDocument>($"{collectionPrefix}.triggers");
        var firedTriggers = database.GetCollection<BsonDocument>($"{collectionPrefix}.firedTriggers");

        await triggers.Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys
                    .Ascending("NextFireTime")
                    .Ascending("State")
                    .Ascending("_id.InstanceName"),
                new CreateIndexOptions { Name = "ix_quartz_trigger_nextfire_state_instance" }),
            cancellationToken: ct);
        await triggers.Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys
                    .Ascending("JobKey")
                    .Ascending("State"),
                new CreateIndexOptions { Name = "ix_quartz_trigger_job_state" }),
            cancellationToken: ct);

        // The store manages its own lock TTL index. Do not add a fired-trigger TTL index here:
        // fired records are required for Quartz recovery and no retention period has been configured.
        _ = firedTriggers;
    }
}
