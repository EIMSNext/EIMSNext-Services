using Microsoft.Extensions.DependencyInjection;

using MongoDB.Driver;

using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Persistence
{
    public static class ServiceCollectionExtensions
    {       
        public static WorkflowOptions UseMongoDB(
            this WorkflowOptions options,
            Func<IServiceProvider, IMongoDatabase> createDatabase)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (createDatabase == null) throw new ArgumentNullException(nameof(createDatabase));

            options.UsePersistence(sp =>
            {
                var db = createDatabase(sp);
                return new MongoPersistenceProvider(db);
            });
            options.Services.AddTransient<IWorkflowPurger>(sp =>
            {
                var db = createDatabase(sp);
                return new WorkflowPurger(db);
            });

            return options;
        }
    }
}
