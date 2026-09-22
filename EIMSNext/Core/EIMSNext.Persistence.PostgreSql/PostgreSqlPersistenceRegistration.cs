using EIMSNext.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EIMSNext.Persistence.PostgreSql;

/// <summary>
/// PostgreSQL 持久化配置接线。业务宿主的 <see cref="PostgreSqlDbContext"/>
/// 由 Autofac lifetime scope 注册，这里只绑定共享 PostgreSQL 配置与 options 约定。
/// </summary>
public static class PostgreSqlPersistenceRegistration
{
    /// <summary>
    /// 注册 PostgreSQL 持久化所需的全部服务。
    /// </summary>
    public static IServiceCollection AddPostgreSqlPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddPostgreSqlConfiguration(configuration);
        return services;
    }

    /// <summary>
    /// 配置业务主上下文的 PostgreSQL options。上下文本身由宿主的 Autofac lifetime scope 注册。
    /// </summary>
    public static void ConfigurePostgreSql(
        DbContextOptionsBuilder options,
        PostgreSqlOptions settings)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(settings);
        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
            throw new InvalidOperationException("PostgreSql:ConnectionString is required.");

        options.UseNpgsql(settings.ConnectionString, npgsql =>
        {
            npgsql.EnableRetryOnFailure(settings.MaxRetryCount);
            npgsql.MigrationsHistoryTable("__EfMigrationsHistory");
        });
        options.UseEimsJsonPathOperators();
        options.EnableSensitiveDataLogging(settings.EnableSensitiveDataLogging);
    }
}
