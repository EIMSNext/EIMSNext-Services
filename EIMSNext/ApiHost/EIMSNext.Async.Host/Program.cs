using System.Diagnostics;
using EIMSNext.ApiCore;
using EIMSNext.Async.Host.Extensions;
using EIMSNext.Async.Quartz;
using EIMSNext.Async.RabbitMQ;
using EIMSNext.Async.Tasks;
using EIMSNext.Component;
using Microsoft.AspNetCore.Http;
using Quartz;
using Serilog;

Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

var appBasePath = AppContext.BaseDirectory;
var isService = !(Debugger.IsAttached || args.Contains("--console"));
var logDirectory = Path.Combine(appBasePath, "Logs");
Directory.CreateDirectory(logDirectory);

try
{
    var builder = Host.CreateDefaultBuilder(args)
        .UseContentRoot(appBasePath);

    builder.UseAutofac<AutofacRegisterModule>();

    builder.UseSerilog((ctx, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "EIMSNext.Async.Host")
            .WriteTo.Console()
           .WriteTo.File(Path.Combine(logDirectory, "quartz-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 31,
               outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    );

    builder.ConfigureServices((hostContext, services) =>
    {
        services.AddBasicServices(hostContext.Configuration);
        services.AddCustomCache(hostContext.Configuration);
        services.AddServiceComponents();
        services.AddDefaultMef(EIMSNext.Common.Constants.BaseDirectory, "*Plugin.dll");
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

        if (isService)
        {
            builder.UseWindowsService();
        }
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
