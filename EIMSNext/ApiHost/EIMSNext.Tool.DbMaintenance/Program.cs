using EIMSNext.Common;
using EIMSNext.Tool.DbMaintenance;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
var dryRun = args.Contains("--dry-run", StringComparer.Ordinal);
var verifyOnly = args.Contains("--verify", StringComparer.Ordinal);
var acceptChecksumChange = args.Contains("--accept-checksum-change", StringComparer.Ordinal);
var targetVersion = ReadOption(args, "--target-version");
var configurationArgs = args.Where(x =>
    x is not "--dry-run"
      and not "--verify"
      and not "--accept-checksum-change"
      and not "--target-version").ToArray();
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
    await host.Services.GetRequiredService<PostgreSqlMaintenanceRunner>()
        .ApplyAsync(dryRun, verifyOnly, targetVersion, acceptChecksumChange);
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Database maintenance failed: {exception.Message}");
    return 1;
}

/// <summary>
/// 读取形如 <c>--name value</c> 的选项值。缺少取值时直接抛错，
/// 避免把下一条参数误当成值后静默执行到错误版本。
/// </summary>
static string? ReadOption(string[] arguments, string name)
{
    var index = Array.FindIndex(arguments, x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
    if (index < 0) return null;
    if (index + 1 >= arguments.Length || arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
        throw new ArgumentException($"{name} requires a value, e.g. {name} 001_CreateTables");
    return arguments[index + 1];
}
