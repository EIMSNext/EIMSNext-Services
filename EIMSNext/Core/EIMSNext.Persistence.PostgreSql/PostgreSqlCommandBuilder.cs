using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace EIMSNext.Persistence.PostgreSql;

/// <summary>创建绑定到 EF Core 上下文连接的 PostgreSQL 命令。</summary>
public static class PostgreSqlCommandBuilder
{
    /// <summary>
    /// 创建并绑定命令参数。连接生命周期由 DbContext 管理，命令由调用方释放。
    /// </summary>
    public static NpgsqlCommand Create(
        DbContext dbContext,
        string sql,
        string? corpId = null,
        IReadOnlyList<object?>? parameters = null)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            connection.Open();

        var command = (NpgsqlCommand)connection.CreateCommand();
        command.CommandText = sql;
        if (dbContext.Database.CurrentTransaction is { } transaction)
            command.Transaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        if (corpId is not null)
        {
            command.Parameters.AddWithValue("corpId", corpId).DataTypeName = EIMSNextModelConfiguration.CaseInsensitiveType;
        }

        for (var index = 0; index < (parameters?.Count ?? 0); index++)
        {
            var value = parameters![index] ?? DBNull.Value;
            var parameter = command.Parameters.AddWithValue($"p{index}", value);

            // Npgsql 默认把 string 参数按 text 发送，而字符列已是 citext：PG 会把 citext 列
            // 降级成 (col)::text 比较，索引条件随之消失（实测 Index Cond 变 Filter）。
            if (value is string) parameter.DataTypeName = EIMSNextModelConfiguration.CaseInsensitiveType;
            else if ((value is string[] || value is IEnumerable<string>) && UsesCitextArrayColumn(sql))
                parameter.DataTypeName = "citext[]";
        }

        return command;
    }

    private static bool UsesCitextArrayColumn(string sql)
        => sql.Contains("PermissionGroupIds", StringComparison.Ordinal)
           || sql.Contains("AppDepartmentIds", StringComparison.Ordinal)
           || sql.Contains("AppEmployeeGroupIds", StringComparison.Ordinal)
           || sql.Contains("ContactDepartmentIds", StringComparison.Ordinal)
           || sql.Contains("ContactEmployeeGroupIds", StringComparison.Ordinal)
           || sql.Contains("\"AppIds\"", StringComparison.Ordinal);
}
