using EIMSNext.Common;
using EIMSNext.Tool.DbMaintenance;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
var dryRun = args.Contains("--dry-run", StringComparer.Ordinal);
var verifyOnly = args.Contains("--verify", StringComparer.Ordinal);
var configurationArgs = args.Where(x => x is not "--dry-run" and not "--verify").ToArray();
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = configurationArgs,
    ContentRootPath = AppContext.BaseDirectory
});
builder.Services.AddPostgreSqlConfiguration(builder.Configuration);
builder.Services.AddSingleton<PostgreSqlMaintenanceRunner>();
using var host = builder.Build();
try
{
    await host.Services.GetRequiredService<PostgreSqlMaintenanceRunner>().ApplyAsync(dryRun, verifyOnly);
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Database maintenance failed: {exception.Message}");
    return 1;
}

