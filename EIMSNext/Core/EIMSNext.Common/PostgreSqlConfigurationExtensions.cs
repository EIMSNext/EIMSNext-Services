using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EIMSNext.Common;

public static class PostgreSqlConfigurationExtensions
{
    public static IServiceCollection AddPostgreSqlConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PostgreSqlOptions>()
            .Configure(options => configuration.GetSection(PostgreSqlOptions.SectionName).Bind(options))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "PostgreSql:ConnectionString is required.")
            .Validate(options => options.MaxRetryCount >= 0, "PostgreSql:MaxRetryCount cannot be negative.")
            .ValidateOnStart();
        return services;
    }
}
