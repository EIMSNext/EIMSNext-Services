using System.Security.Cryptography;
using System.Text.RegularExpressions;
using EIMSNext.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;

namespace EIMSNext.Tool.DbMaintenance;

public sealed class PostgreSqlMaintenanceRunner(IOptions<PostgreSqlOptions> options, IConfiguration configuration)
{
    /// <summary>
    /// 应用迁移。<paramref name="targetVersion"/> 非空时只执行不超过该版本的脚本，
    /// 用于「升级到指定版本」或「灰度环境停留在旧版本」的场景。
    /// </summary>
    public async Task ApplyAsync(
        bool dryRun = false,
        bool verifyOnly = false,
        string? targetVersion = null,
        bool acceptChecksumChange = false,
        CancellationToken cancellationToken = default)
    {
        var configured = configuration["DbMaintenance:SqlDirectory"] ?? "Sql";
        var directory = Path.GetFullPath(configured, AppContext.BaseDirectory);
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException(directory);
        var scripts = new List<Script>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.sql").OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            var version = Path.GetFileNameWithoutExtension(path);
            if (!Regex.IsMatch(version, @"^\d{3,}_[A-Za-z0-9_]+$") || version.Length > 128)
                throw new InvalidOperationException($"Invalid SQL version filename: {version}");
            var sql = await File.ReadAllTextAsync(path, cancellationToken);
            var hash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path, cancellationToken)));
            scripts.Add(new(version, sql, hash));
        }
        if (scripts.Count == 0) throw new InvalidOperationException("No SQL scripts found.");
        if (scripts.GroupBy(x => x.Version.Split('_')[0]).Any(group => group.Count() > 1))
            throw new InvalidOperationException("SQL version prefixes must be unique.");

        // 目标版本必须真实存在于脚本目录，避免因为拼错版本号而静默地什么都不执行。
        if (!string.IsNullOrWhiteSpace(targetVersion))
        {
            if (!scripts.Any(x => string.Equals(x.Version, targetVersion, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Target version not found: {targetVersion}");

            var skipped = scripts
                .Where(x => string.CompareOrdinal(x.Version, targetVersion) > 0)
                .Select(x => x.Version)
                .ToArray();
            if (skipped.Length != 0)
                Console.WriteLine($"Target version {targetVersion}; skipping {skipped.Length} newer script(s): {string.Join(", ", skipped)}");
            scripts = scripts.Where(x => string.CompareOrdinal(x.Version, targetVersion) <= 0).ToList();
        }

        if (dryRun)
        {
            foreach (var script in scripts) Console.WriteLine($"{script.Version} {script.Checksum}");
            return; // Offline preview never opens a database connection.
        }

        var settings = new NpgsqlConnectionStringBuilder(options.Value.ConnectionString)
        {
            // A session advisory lock must never leak back into a connection pool.
            Pooling = false
        };
        await using var connection = new NpgsqlConnection(settings.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using (var acquire = new NpgsqlCommand("SELECT pg_advisory_lock(hashtext(current_database()), hashtext('EIMSNext.DbMaintenance'))", connection))
            await acquire.ExecuteNonQueryAsync(cancellationToken);
        // Closing this dedicated connection releases the lock even if cancelled.
        var exists = false;
        await using (var check = new NpgsqlCommand("SELECT to_regclass('\"__DbMaintenanceHistory\"') IS NOT NULL", connection))
            exists = (bool)(await check.ExecuteScalarAsync(cancellationToken))!;

        if (!exists)
        {
            if (verifyOnly) throw new InvalidOperationException("Database is not initialized.");
            await using var create = new NpgsqlCommand("""
                CREATE TABLE "__DbMaintenanceHistory" (
                    "Version" varchar(128) PRIMARY KEY,
                    "AppliedAt" timestamptz NOT NULL,
                    "Checksum" varchar(128) NOT NULL
                )
                """, connection);
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        var history = new Dictionary<string, string>(StringComparer.Ordinal);
        await using (var read = new NpgsqlCommand("SELECT \"Version\", \"Checksum\" FROM \"__DbMaintenanceHistory\"", connection))
        {
            await using var reader = await read.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) history.Add(reader.GetString(0), reader.GetString(1));
        }

        // Validate the whole history before executing any pending script.
        foreach (var entry in history)
        {
            var script = scripts.SingleOrDefault(x => x.Version == entry.Key);
            if (script is null) throw new InvalidOperationException($"Applied script is missing: {entry.Key}");
            if (string.Equals(script.Checksum, entry.Value, StringComparison.OrdinalIgnoreCase)) continue;

            // 已执行脚本的内容被改动。默认阻断：这说明有人在迁移之后回头改了历史脚本，
            // 而该改动不会自动作用到已迁移的库，环境之间会悄悄产生结构差异。
            // 确属有意修正时用 --accept-checksum-change 显式放行，并同步更新台账校验和。
            if (!acceptChecksumChange)
                throw new InvalidOperationException(
                    $"Applied script checksum changed: {entry.Key}. " +
                    "若确认是有意修正，请带 --accept-checksum-change 重新执行。");

            await using var update = new NpgsqlCommand(
                "UPDATE \"__DbMaintenanceHistory\" SET \"Checksum\" = @checksum WHERE \"Version\" = @version", connection);
            update.Parameters.AddWithValue("version", entry.Key);
            update.Parameters.AddWithValue("checksum", script.Checksum);
            await update.ExecuteNonQueryAsync(cancellationToken);
            history[entry.Key] = script.Checksum;
            Console.WriteLine($"Checksum updated for {entry.Key}");
        }
        var latest = history.Keys.Order(StringComparer.Ordinal).LastOrDefault();
        var pending = scripts.Where(x => !history.ContainsKey(x.Version)).ToArray();
        if (latest is not null && pending.Any(x => string.CompareOrdinal(x.Version, latest) < 0))
            throw new InvalidOperationException("Pending scripts must be newer than all applied scripts.");
        if (verifyOnly && pending.Length != 0)
            throw new InvalidOperationException($"Database has {pending.Length} pending SQL scripts.");

        foreach (var script in pending)
        {
            // Destructive migrations are explicitly declared, not inferred by a
            // regex that pretends to be a PostgreSQL parser.
            if (Regex.IsMatch(script.Sql, @"(?im)^\s*--\s*destructive:\s*true\s*$")
                && !configuration.GetValue<bool>("DbMaintenance:AllowDestructiveChanges"))
                throw new InvalidOperationException($"Destructive migration is disabled: {script.Version}");

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using (var command = new NpgsqlCommand(script.Sql, connection, transaction))
                await command.ExecuteNonQueryAsync(cancellationToken);
            await using (var record = new NpgsqlCommand("""
                INSERT INTO "__DbMaintenanceHistory" ("Version", "AppliedAt", "Checksum")
                VALUES (@version, now(), @checksum)
                """, connection, transaction))
            {
                record.Parameters.AddWithValue("version", script.Version);
                record.Parameters.AddWithValue("checksum", script.Checksum);
                await record.ExecuteNonQueryAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            Console.WriteLine($"Applied {script.Version}");
        }
        Console.WriteLine(verifyOnly ? "Database versions verified." : $"Applied {pending.Length} migration(s).");
    }

    private sealed record Script(string Version, string Sql, string Checksum);
}

