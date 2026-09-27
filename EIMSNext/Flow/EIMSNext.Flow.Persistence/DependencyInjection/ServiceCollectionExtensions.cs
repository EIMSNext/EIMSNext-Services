using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Persistence;

public static class ServiceCollectionExtensions
{
    public static WorkflowOptions UsePostgreSql<TContext>(this WorkflowOptions options)
        where TContext : DbContext, IWfDbContext
    {
        ArgumentNullException.ThrowIfNull(options);
        // WorkflowCore owns a long-lived provider. Its factory creates a separate
        // context for every operation, including background workers and retries.
        options.UsePersistence(sp =>
        {
            var factory = sp.GetRequiredService<IDbContextFactory<TContext>>();
            return new PostgreSqlPersistenceProvider(
                async cancellationToken => await factory.CreateDbContextAsync(cancellationToken),
                sp.GetRequiredService<ILogger<PostgreSqlPersistenceProvider>>());
        });
        options.Services.AddTransient<IWorkflowInstancePurger>(sp =>
        {
            var factory = sp.GetRequiredService<IDbContextFactory<TContext>>();
            return new WorkflowPurger(async cancellationToken => await factory.CreateDbContextAsync(cancellationToken));
        });
        options.Services.AddTransient<IWorkflowPurger>(sp => sp.GetRequiredService<IWorkflowInstancePurger>());
        return options;
    }
}
