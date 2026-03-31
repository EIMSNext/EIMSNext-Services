using System.Diagnostics;

using EIMSNext.Async.Core;
using EIMSNext.Async.Core.Jobs;

using Quartz;

using RabbitMQ.Client;
using EIMSNext.ApiCore;
using Serilog;
using EIMSNext.Component;

Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory); //服务启动时，当前目录默认为system32

var appBasePath = AppContext.BaseDirectory;
var isService = !(Debugger.IsAttached || args.Contains("--console"));
var logDirectory = Path.Combine(appBasePath, "Logs");
Directory.CreateDirectory(logDirectory);

try
{
    var builder = Host.CreateDefaultBuilder(args)
        .UseContentRoot(appBasePath);

    builder.UseSerilog((hostingContext, config) =>
        config.ReadFrom.Configuration(hostingContext.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "EIMSNext.Async.Host")
            .WriteTo.Console()
            .WriteTo.File(
                Path.Combine(logDirectory, "quartz-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 31,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] {Message:lj}{NewLine}{Exception}"
            )
    );

    builder.ConfigureServices((hostContext, services) =>
    {
        services.AddBasicServices(hostContext.Configuration);
        services.AddCustomCache(hostContext.Configuration);
        services.AddServiceComponents();
        services.AddDefaultMef(EIMSNext.Common.Constants.BaseDirectory, "*Plugin.dll");

        services.AddSingleton<IConnection>(sp =>
        {
            var factory = new ConnectionFactory
            {
                HostName = "localhost",
                UserName = "guest",
                Password = "guest"
            };
            return factory.CreateConnection();
        });

        services.AddConsumers();

        services.AddJobs();

        services.AddQuartz(q =>
        {
            q.UsePersistentStore(store =>
            {
                store.RetryInterval = TimeSpan.FromSeconds(15);
                store.UseSystemTextJsonSerializer();
                store.UseSqlServer(sqlServer =>
                {
                    sqlServer.ConnectionString = hostContext.Configuration
                        .GetConnectionString("Quartz:ConnectionString")
                        ?? throw new InvalidOperationException("Missing Quartz connection string");
                    sqlServer.TablePrefix = "QRTZ_";
                });
            });

            q.AddQuartzTriggers(hostContext.Configuration);
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