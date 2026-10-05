using EIMSNext.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EIMSNext.Tool.DbMaintenance;

/// <summary>
/// 一次「执行」的完整路径：把参数解析成「配置参数 + 开关」，构建 Host 后应用一次迁移。
/// 命令行模式与交互式菜单共用这段代码，两条入口的行为完全一致。
/// </summary>
public static class MaintenanceRunner
{
    public static async Task<int> ExecuteAsync(string[] arguments, bool showDetails = false)
    {
        var command = Parse(arguments);
        try
        {
            var builder = CreateBuilder(command.ConfigurationArgs);
            builder.Services.AddPostgreSqlConfiguration(builder.Configuration);
            builder.Services.AddSingleton<PostgreSqlMaintenanceRunner>();
            using var host = builder.Build();
            await host.Services.GetRequiredService<PostgreSqlMaintenanceRunner>()
                .ApplyAsync(command.DryRun, command.VerifyOnly, command.TargetVersion, command.AcceptChecksumChange);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Database maintenance failed: {exception.Message}");
            // 命令行模式保持安静（CI 里不要灌堆栈）；菜单模式是给人看的，打印详情方便定位。
            if (showDetails) Console.Error.WriteLine(exception);
            return 1;
        }
    }

    /// <summary>
    /// 读取按当前参数计算出来的生效配置，仅用于向用户展示连接目标等信息，不执行任何迁移。
    /// 配置源必须与 <see cref="ExecuteAsync"/> 完全一致，否则菜单里看到的库不是真正连的那个。
    /// </summary>
    public static IConfigurationRoot ReadConfiguration(string[] arguments) =>
        CreateBuilder(Parse(arguments).ConfigurationArgs).Configuration;

    private static HostApplicationBuilder CreateBuilder(string[] configurationArgs) =>
        Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = configurationArgs,
            ContentRootPath = AppContext.BaseDirectory
        });

    private static Command Parse(string[] arguments)
    {
        var dryRun = arguments.Contains("--dry-run", StringComparer.Ordinal);
        var verifyOnly = arguments.Contains("--verify", StringComparer.Ordinal);
        var acceptChecksumChange = arguments.Contains("--accept-checksum-change", StringComparer.Ordinal);
        var targetVersion = ReadOption(arguments, "--target-version");
        var configurationArgs = arguments.Where(x =>
            x is not "--dry-run"
              and not "--verify"
              and not "--accept-checksum-change"
              and not "--target-version").ToArray();
        return new(configurationArgs, dryRun, verifyOnly, targetVersion, acceptChecksumChange);
    }

    /// <summary>
    /// 读取形如 <c>--name value</c> 的选项值。缺少取值时直接抛错，
    /// 避免把下一条参数误当成值后静默执行到错误版本。
    /// </summary>
    private static string? ReadOption(string[] arguments, string name)
    {
        var index = Array.FindIndex(arguments, x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return null;
        if (index + 1 >= arguments.Length || arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException($"{name} requires a value, e.g. {name} 001_CreateTables");
        return arguments[index + 1];
    }

    private sealed record Command(
        string[] ConfigurationArgs,
        bool DryRun,
        bool VerifyOnly,
        string? TargetVersion,
        bool AcceptChecksumChange);
}
