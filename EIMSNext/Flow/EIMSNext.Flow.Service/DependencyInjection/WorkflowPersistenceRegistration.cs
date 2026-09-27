using EIMSNext.Common;
using EIMSNext.Flow.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EIMSNext.Flow.Service;

public static class WorkflowPersistenceRegistration
{
    public static IServiceCollection AddWorkflowPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddPostgreSqlConfiguration(configuration);
        services.AddDbContextFactory<WfDbContext>((provider, options) =>
        {
            var settings = provider.GetRequiredService<IOptions<PostgreSqlOptions>>().Value;
            options.UseNpgsql(settings.ConnectionString, postgres => postgres.EnableRetryOnFailure(settings.MaxRetryCount));
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            options.EnableSensitiveDataLogging(settings.EnableSensitiveDataLogging);
        });
        services.AddScoped<IWfDbContext>(provider => provider.GetRequiredService<WfDbContext>());
        return services;
    }
}
