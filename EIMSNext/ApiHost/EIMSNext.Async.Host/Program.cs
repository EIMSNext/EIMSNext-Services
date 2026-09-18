using EIMSNext.ApiCore;
using EIMSNext.Plugin.Runtime;
using EIMSNext.Mef;
using EIMSNext.Async.Host;
using EIMSNext.Async.Host.Extensions;
using EIMSNext.Async.Quartz;
using EIMSNext.Async.RabbitMQ;
using EIMSNext.Async.Tasks;
using EIMSNext.Component;
using EIMSNext.Flow.Service;
using EIMSNext.Persistence.PostgreSql;
using Quartz;
using Quartz.Impl;
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
        services.AddWorkflowPersistence(hostContext.Configuration);
        services.AddPostgreSqlPersistence(hostContext.Configuration);
        services.AddBasicServices(hostContext.Configuration);
        services.AddCustomCache(hostContext.Configuration);
        services.AddServiceComponents();
        services.AddGlobalMef(EIMSNext.Common.Constants.BaseDirectory);
        services.AddPluginRuntime(EIMSNext.Common.Constants.BaseDirectory);
        services.AddRabbitMqMessaging(hostContext.Configuration);
        services.AddAsyncTaskConsumers();
        services.AddAsyncQuartzJobs();

        services.AddQuartz(q =>
        {
            var quartzConfiguration = hostContext.Configuration.GetSection("Quartz");
            var connectionString = hostContext.Configuration.GetSection("PostgreSql").GetValue<string>("ConnectionString")
                ?? throw new InvalidOperationException("Missing PostgreSQL connection string");

            q.UsePersistentStore(store =>
            {
                store.UsePostgreSql(postgres =>
                {
                    postgres.ConnectionString = connectionString;
                    postgres.TablePrefix = quartzConfiguration.GetValue<string>("TablePrefix") ?? "qrtz_";
                });
                store.SetProperty(
                    StdSchedulerFactory.PropertySchedulerInstanceName,
                    quartzConfiguration.GetValue<string>("InstanceName") ?? "EIMSNextAsync");
                store.SetProperty(
                    StdSchedulerFactory.PropertySchedulerInstanceId,
                    quartzConfiguration.GetValue<string>("InstanceId") ?? "AUTO");
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

