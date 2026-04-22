using EIMSNext.ApiCore;
using EIMSNext.ApiCore.Plugin;
using EIMSNext.Async.Host.Extensions;
using EIMSNext.Async.Quartz;
using EIMSNext.Async.RabbitMQ;
using EIMSNext.Async.Tasks;
using EIMSNext.Component;
using Quartz;
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

        services.AddQuartz(q =>
        {
            q.UsePersistentStore(store =>
            {
                store.RetryInterval = TimeSpan.FromSeconds(15);
                store.UseSystemTextJsonSerializer();
                store.UseSqlServer(sqlServer =>
                {
                    sqlServer.ConnectionString = hostContext.Configuration.GetSection("Quartz")?.GetValue<string>("ConnectionString")
                        ?? throw new InvalidOperationException("Missing Quartz connection string");
                    sqlServer.TablePrefix = "QRTZ_";
                });
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
