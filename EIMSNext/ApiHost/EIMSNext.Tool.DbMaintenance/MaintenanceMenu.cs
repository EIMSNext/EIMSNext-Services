using Microsoft.Extensions.Configuration;
using Npgsql;

namespace EIMSNext.Tool.DbMaintenance;

/// <summary>
/// 交互式菜单。两种场景进入：不带任何命令行参数（典型场景：VS 里右键运行），
/// 或显式写了 <c>-i</c> / <c>--interactive</c>。
///
/// 这么做有两个目的：
/// 1. 控制台窗口不会执行完就关掉——用户永远看得见结果和报错；
/// 2. 不用记 <c>--dry-run</c> 之类的开关，菜单里选就行。
///
/// 菜单里的每次选择都会被拼成命令行参数，再交给 <see cref="MaintenanceRunner"/> 执行，
/// 因此不会出现「菜单模式一套行为、命令行模式另一套行为」。
/// </summary>
public static class MaintenanceMenu
{
    public static async Task<int> RunAsync()
    {
        string? overriddenConnectionString = null;
        var acceptChecksumChange = false;

        while (true)
        {
            // 连接串覆盖值以命令行配置的形式往下传，优先级高于 appsettings.json 与环境变量。
            string[] extras = string.IsNullOrWhiteSpace(overriddenConnectionString)
                ? []
                : [$"--PostgreSql:ConnectionString={overriddenConnectionString}"];

            PrintHeader(extras);
            PrintMenu(acceptChecksumChange);

            var input = ReadLine();
            if (input is null) return 0; // 非交互或 stdin 已关闭：不要死循环刷屏。

            var choice = input.Trim();
            if (choice.Length == 0) continue; // 直接回车：重新显示菜单。

            switch (choice.ToLowerInvariant())
            {
                case "1":
                    await ExecuteAsync(extras, acceptChecksumChange, "--dry-run");
                    break;
                case "2":
                    await ExecuteAsync(extras, acceptChecksumChange, "--verify");
                    break;
                case "3":
                    await ExecuteAsync(extras, acceptChecksumChange);
                    break;
                case "4":
                    var targetVersion = AskTargetVersion(extras);
                    if (targetVersion is not null)
                        await ExecuteAsync(extras, acceptChecksumChange, "--target-version", targetVersion);
                    break;
                case "c":
                    overriddenConnectionString = AskConnectionString(overriddenConnectionString);
                    continue;
                case "s":
                    acceptChecksumChange = !acceptChecksumChange;
                    continue;
                case "q":
                case "quit":
                case "exit":
                    return 0;
                default:
                    Console.WriteLine("无效选项，请重新输入。");
                    break;
            }

            if (!Pause()) return 0;
        }
    }

    private static async Task ExecuteAsync(string[] extras, bool acceptChecksumChange, params string[] switches)
    {
        var arguments = new List<string>(extras);
        arguments.AddRange(switches);
        if (acceptChecksumChange) arguments.Add("--accept-checksum-change");

        Console.WriteLine();
        await MaintenanceRunner.ExecuteAsync(arguments.ToArray(), showDetails: true);
    }

    private static void PrintHeader(string[] extras)
    {
        Console.WriteLine();
        Console.WriteLine("========== EIMSNext 数据库维护 ==========");
        var configuration = MaintenanceRunner.ReadConfiguration(extras);
        var connectionString = configuration["PostgreSql:ConnectionString"];
        Console.WriteLine(string.IsNullOrWhiteSpace(connectionString)
            ? "目标库    : 未配置 PostgreSql:ConnectionString"
            : $"目标库    : {Describe(connectionString)}");
        Console.WriteLine($"脚本目录  : {ResolveSqlDirectory(configuration)}");
        Console.WriteLine("----------------------------------------");
    }

    private static void PrintMenu(bool acceptChecksumChange)
    {
        Console.WriteLine("  [1] 预览将要执行的脚本      (--dry-run，不连库)");
        Console.WriteLine("  [2] 校验迁移链状态          (--verify，不改库)");
        Console.WriteLine("  [3] 执行迁移                (升级到最新版本)");
        Console.WriteLine("  [4] 执行到指定版本          (--target-version)");
        Console.WriteLine("  [C] 修改目标库连接串");
        Console.WriteLine($"  [S] 允许已执行脚本内容变更  (--accept-checksum-change：{(acceptChecksumChange ? "开" : "关")})");
        Console.WriteLine("  [Q] 退出");
        Console.WriteLine();
        Console.Write("请输入选项并回车：");
    }

    /// <summary>从脚本目录里挑一个版本号；返回 null 表示用户取消。</summary>
    private static string? AskTargetVersion(string[] extras)
    {
        var directory = ResolveSqlDirectory(MaintenanceRunner.ReadConfiguration(extras));
        if (!Directory.Exists(directory))
        {
            Console.WriteLine($"脚本目录不存在：{directory}");
            return null;
        }

        var versions = Directory.EnumerateFiles(directory, "*.sql")
            .Select(Path.GetFileNameWithoutExtension)
            .Select(x => x!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Console.WriteLine();
        Console.WriteLine("可用脚本：");
        for (var i = 0; i < versions.Length; i++) Console.WriteLine($"   {i + 1,2}. {versions[i]}");
        Console.Write("请输入序号或完整版本号（直接回车取消）：");

        var input = ReadLine()?.Trim();
        if (string.IsNullOrEmpty(input)) return null;

        var version = int.TryParse(input, out var index) && index >= 1 && index <= versions.Length
            ? versions[index - 1]
            : input;
        if (!versions.Contains(version, StringComparer.Ordinal))
        {
            Console.WriteLine($"脚本不存在：{version}");
            return null;
        }
        return version;
    }

    /// <summary>让用户输入新的目标库连接串；输入空串表示清空覆盖、回落至配置与环境变量。</summary>
    private static string? AskConnectionString(string? current)
    {
        Console.WriteLine();
        Console.WriteLine("输入新连接串，例如 Host=localhost;Port=5432;Database=EIMS;Username=postgres;Password=sa123");
        Console.Write("（直接回车 = 清空覆盖，回落至 appsettings.json / 环境变量）> ");

        var input = ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            Console.WriteLine("已清空覆盖值。");
            return null;
        }

        try
        {
            _ = new NpgsqlConnectionStringBuilder(input);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"连接串无法解析，已保留原值：{exception.Message}");
            return current;
        }

        Console.WriteLine($"目标库已切换为：{Describe(input)}");
        return input;
    }

    private static string ResolveSqlDirectory(IConfiguration configuration)
    {
        var configured = configuration["DbMaintenance:SqlDirectory"] ?? "Sql";
        return Path.GetFullPath(configured, AppContext.BaseDirectory);
    }

    /// <summary>只展示库的位置信息，不回显密码。</summary>
    private static string Describe(string connectionString)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            var user = string.IsNullOrWhiteSpace(builder.Username) ? "" : $"{builder.Username}@";
            return $"{user}{builder.Host}:{builder.Port}/{builder.Database}";
        }
        catch
        {
            return "(连接串无法解析)";
        }
    }

    private static string? ReadLine()
    {
        var line = Console.ReadLine();
        if (line is null) Console.WriteLine();
        return line;
    }

    private static bool Pause()
    {
        Console.WriteLine();
        Console.Write("按 Enter 返回菜单...");
        return Console.ReadLine() is not null;
    }
}
