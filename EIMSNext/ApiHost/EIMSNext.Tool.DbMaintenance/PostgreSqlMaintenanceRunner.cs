using System.Security.Cryptography;
using System.Text.RegularExpressions;
using EIMSNext.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;

namespace EIMSNext.Tool.DbMaintenance;

public sealed class PostgreSqlMaintenanceRunner(IOptions<PostgreSqlOptions> options, IConfiguration configuration)
{
    public async Task ApplyAsync(bool dryRun = false, bool verifyOnly = false, CancellationToken cancellationToken = default)
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
            if (!string.Equals(script.Checksum, entry.Value, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Applied script checksum changed: {entry.Key}");
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

