using Microsoft.Extensions.DependencyInjection;

using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Persistence
{
    public static class ServiceCollectionExtensions
    {       
        public static WorkflowOptions UseMongoDB(
            this WorkflowOptions options,
            Func<IServiceProvider, IWfDbContext> createDbContext)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (createDbContext == null) throw new ArgumentNullException(nameof(createDbContext));

            options.UsePersistence(sp =>
            {
                var dbContext = createDbContext(sp);
                return new MongoPersistenceProvider(dbContext);
            });
            options.Services.AddTransient<IWorkflowInstancePurger>(sp =>
            {
                var dbContext = createDbContext(sp);
                return new WorkflowPurger(dbContext);
            });
            options.Services.AddTransient<IWorkflowPurger>(sp => sp.GetRequiredService<IWorkflowInstancePurger>());

            return options;
        }
    }
}
