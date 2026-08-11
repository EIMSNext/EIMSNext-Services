using EIMSNext.ApiCore;
using EIMSNext.ApiCore.Plugin;
using EIMSNext.Async.Host;
using EIMSNext.Async.Host.Extensions;
using EIMSNext.Async.Quartz;
using EIMSNext.Async.RabbitMQ;
using EIMSNext.Async.Tasks;
using EIMSNext.Component;
using Quartz;
using Quartz.Impl;
using Quartz.Store.MongoDb;
using Serilog;

Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

var appBasePath = AppContext.BaseDirectory;
var logDirectory = Path.Combine(appBasePath, "Logs");
Directory.CreateDirectory(logDirectory);

try
{
    var builder = Host.CreateDefaultBuilder(args)
        .UseContentRoot(appBasePath)
        .UseWindowsService(cfg =>
        {
            cfg.ServiceName = "EIMSNext Async Service";
        });

    builder.UseAutofac<AutofacRegisterModule>();

    builder.UseSerilog((ctx, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration)
    );

    builder.ConfigureServices((hostContext, services) =>
    {
        services.AddBasicServices(hostContext.Configuration);
        services.AddCustomCache(hostContext.Configuration);
        services.AddServiceComponents();
        services.AddGlobalMef(EIMSNext.Common.Constants.BaseDirectory);
        services.AddPluginRuntime(EIMSNext.Common.Constants.BaseDirectory);
        services.AddRabbitMqMessaging(hostContext.Configuration);
        services.AddAsyncTaskConsumers();
        services.AddAsyncQuartzJobs();
        services.AddSingleton<QuartzMongoIndexInitializer>();

        services.AddQuartz(q =>
        {
            var quartzConfiguration = hostContext.Configuration.GetSection("Quartz");
            var connectionString = quartzConfiguration.GetValue<string>("ConnectionString")
                ?? throw new InvalidOperationException("Missing Quartz MongoDB connection string");

            q.UsePersistentStore<MongoDbJobStore>(store =>
            {
                store.SetProperty(
                    StdSchedulerFactory.PropertySchedulerInstanceName,
                    quartzConfiguration.GetValue<string>("InstanceName") ?? "EIMSNextAsync");
                store.SetProperty(
                    StdSchedulerFactory.PropertySchedulerInstanceId,
                    quartzConfiguration.GetValue<string>("InstanceId") ?? "AUTO");
                store.SetProperty("quartz.jobStore.connectionString", connectionString);
                store.SetProperty(
                    "quartz.jobStore.collectionPrefix",
                    quartzConfiguration.GetValue<string>("CollectionPrefix") ?? "quartz");
                store.SetProperty(
                    "quartz.jobStore.misfireThreshold",
                    quartzConfiguration.GetValue<string>("MisfireThreshold") ?? "60000");
                store.SetProperty(
                    "quartz.jobStore.dbRetryInterval",
                    quartzConfiguration.GetValue<string>("DbRetryInterval") ?? "15000");
                store.UseNewtonsoftJsonSerializer();
            });
            q.AddAsyncQuartzTriggers(hostContext.Configuration);
        });

        services.AddQuartzHostedService(q =>
        {
            q.WaitForJobsToComplete = true;
        });
    });

    var host = builder.Build();
    await host.Services.GetRequiredService<QuartzMongoIndexInitializer>().InitializeAsync();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "HostException: Host terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

return 0;
