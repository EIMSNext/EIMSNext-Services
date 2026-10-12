using System.Collections;
using System.Data.Common;

using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EIMSNext.Persistence.PostgreSql;

/// <summary>
/// Ensures collection parameters used by EF's <c>Contains</c> translation use the
/// PostgreSQL <c>citext[]</c> type. EF maps a scalar property comparison from the
/// property's column mapping, but a captured <c>List&lt;string&gt;</c> is otherwise
/// inferred independently as <c>text[]</c>.
/// </summary>
public sealed class CitextParameterInterceptor : DbCommandInterceptor
{
    public static CitextParameterInterceptor Instance { get; } = new();

    private CitextParameterInterceptor()
    {
    }

    public override DbCommand CommandInitialized(CommandEndEventData eventData, DbCommand command)
    {
        Configure(command);
        return command;
    }

    internal static void Configure(DbCommand command)
    {
        foreach (DbParameter parameter in command.Parameters)
        {
            if ((parameter.Value is string[] || IsStringSequence(parameter.Value)) && UsesCitextArrayColumn(command.CommandText))
            {
                parameter.DbType = System.Data.DbType.Object;
                if (parameter is Npgsql.NpgsqlParameter npgsqlParameter)
                    npgsqlParameter.DataTypeName = "citext[]";
            }
            else if (parameter.Value is string && UsesCitextScalarArrayColumn(command.CommandText, parameter.ParameterName))
            {
                if (parameter is Npgsql.NpgsqlParameter npgsqlParameter)
                    npgsqlParameter.DataTypeName = "citext";
            }
        }
    }

    private static bool IsStringSequence(object? value)
        => value is IEnumerable sequence
           && value is not string
           && sequence.Cast<object?>().All(item => item is string);

    private static bool UsesCitextArrayColumn(string sql)
        => sql.Contains("PermissionGroupIds", StringComparison.Ordinal)
           || sql.Contains("AppDepartmentIds", StringComparison.Ordinal)
           || sql.Contains("AppEmployeeGroupIds", StringComparison.Ordinal)
           || sql.Contains("ContactDepartmentIds", StringComparison.Ordinal)
           || sql.Contains("ContactEmployeeGroupIds", StringComparison.Ordinal)
           || sql.Contains("\"AppIds\"", StringComparison.Ordinal);

    private static bool UsesCitextScalarArrayColumn(string sql, string parameterName)
        => sql.Contains("ANY", StringComparison.OrdinalIgnoreCase)
           && sql.Contains(parameterName, StringComparison.Ordinal)
           && UsesCitextArrayColumn(sql);
}
