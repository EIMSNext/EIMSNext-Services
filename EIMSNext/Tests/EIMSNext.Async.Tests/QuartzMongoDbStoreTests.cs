using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using Quartz;
using Quartz.Impl;
using Quartz.Store.MongoDb;

namespace EIMSNext.Async.Tests;

[TestClass]
public class QuartzMongoDbStoreTests
{
    private static readonly List<ServiceProvider> Providers = [];

    [TestMethod]
    public async Task MongoDbIndexInitializer_CreatesExpectedTriggerIndexes()
    {
        var databaseName = $"QuartzRegression_{Guid.NewGuid():N}";
        var collectionPrefix = $"quartz_{Guid.NewGuid():N}";
        var connectionString = $"mongodb://localhost:27017/{databaseName}";
        var database = new MongoClient(connectionString).GetDatabase(databaseName);

        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Quartz:ConnectionString"] = connectionString,
                    ["Quartz:CollectionPrefix"] = collectionPrefix
                }).Build();
            var hostAssemblyPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "../../../../../ApiHost/EIMSNext.Async.Host/bin/Debug/net10.0/EIMSNext.Async.Host.dll"));
            var hostAssembly = System.Reflection.Assembly.LoadFrom(hostAssemblyPath);
            var initializerType = hostAssembly.GetType("EIMSNext.Async.Host.QuartzMongoIndexInitializer", throwOnError: true)!;
            var initializer = Activator.CreateInstance(
                initializerType,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                binder: null,
                args: [configuration],
                culture: null)!;
            var initializeTask = (Task)initializerType.GetMethod("InitializeAsync")!
                .Invoke(initializer, [CancellationToken.None])!;
            await initializeTask;

            var indexes = await database.GetCollection<BsonDocument>($"{collectionPrefix}.triggers")
                .Indexes.ListAsync();
            var indexNames = (await indexes.ToListAsync()).Select(x => x["name"].AsString).ToList();
            CollectionAssert.Contains(indexNames, "ix_quartz_trigger_nextfire_state_instance");
            CollectionAssert.Contains(indexNames, "ix_quartz_trigger_job_state");
        }
        finally
        {
            await database.Client.DropDatabaseAsync(databaseName);
        }
    }

    [TestMethod]
    public async Task MongoDbStore_PersistsDurableJobAndTriggerAcrossSchedulerRestart()
    {
        var databaseName = $"QuartzRegression_{Guid.NewGuid():N}";
        var collectionPrefix = $"quartz_{Guid.NewGuid():N}";
        var connectionString = $"mongodb://localhost:27017/{databaseName}";
        var jobKey = new JobKey("MongoPersistenceJob", "Regression");
        var triggerKey = new TriggerKey("MongoPersistenceTrigger", "Regression");
        var database = new MongoClient(connectionString).GetDatabase(databaseName);

        try
        {
            // Quartz retains the first DI logger factory in process-global state.
            // Keep both providers alive until the test process exits.
            var firstProvider = BuildProvider(connectionString, collectionPrefix, jobKey, triggerKey);
            var firstScheduler = await firstProvider.GetRequiredService<ISchedulerFactory>().GetScheduler();
            await firstScheduler.Start();
            await firstScheduler.Shutdown(waitForJobsToComplete: true);

            var triggers = database.GetCollection<BsonDocument>($"{collectionPrefix}.triggers");
            Assert.AreEqual(1, await triggers.CountDocumentsAsync(Builders<BsonDocument>.Filter.Empty));

            var secondProvider = BuildProvider(connectionString, collectionPrefix, jobKey, triggerKey);
            var secondScheduler = await secondProvider.GetRequiredService<ISchedulerFactory>().GetScheduler();
            Assert.IsNotNull(await secondScheduler.GetJobDetail(jobKey));
            Assert.IsNotNull(await secondScheduler.GetTrigger(triggerKey));
            await secondScheduler.Start();
            await secondScheduler.Shutdown(waitForJobsToComplete: true);

            Assert.AreEqual(1, await triggers.CountDocumentsAsync(Builders<BsonDocument>.Filter.Empty));
        }
        finally
        {
            await database.Client.DropDatabaseAsync(databaseName);
        }
    }

    private static ServiceProvider BuildProvider(string connectionString, string collectionPrefix, JobKey jobKey, TriggerKey triggerKey)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddQuartz(q =>
        {
            q.UsePersistentStore<MongoDbJobStore>(store =>
            {
                store.SetProperty(StdSchedulerFactory.PropertySchedulerInstanceName, "EIMSNextAsyncRegression");
                store.SetProperty(StdSchedulerFactory.PropertySchedulerInstanceId, "AUTO");
                store.SetProperty("quartz.jobStore.connectionString", connectionString);
                store.SetProperty("quartz.jobStore.collectionPrefix", collectionPrefix);
                store.SetProperty("quartz.jobStore.misfireThreshold", "60000");
                store.SetProperty("quartz.jobStore.dbRetryInterval", "15000");
                store.UseNewtonsoftJsonSerializer();
            });
            q.AddJob<PersistentNoopJob>(options => options.StoreDurably().WithIdentity(jobKey));
            q.AddTrigger(options => options
                .ForJob(jobKey)
                .WithIdentity(triggerKey)
                .StartAt(DateBuilder.FutureDate(1, IntervalUnit.Day)));
        });
        var provider = services.BuildServiceProvider();
        Providers.Add(provider);
        return provider;
    }

    private sealed class PersistentNoopJob : IJob
    {
        public Task Execute(IJobExecutionContext context) => Task.CompletedTask;
    }
}
