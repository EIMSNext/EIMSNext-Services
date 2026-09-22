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
            command.Parameters.AddWithValue("corpId", corpId);

        for (var index = 0; index < (parameters?.Count ?? 0); index++)
            command.Parameters.AddWithValue($"p{index}", parameters![index] ?? DBNull.Value);

        return command;
    }
}
