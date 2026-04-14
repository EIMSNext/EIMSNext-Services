using EIMSNext.Async.Tasks.Consumers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EIMSNext.Async.Tasks
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAsyncTaskConsumers(this IServiceCollection services)
        {
            services.AddHostedService<NotifyDispatchConsumer>();
            services.AddHostedService<SystemMessageConsumer>();
            services.AddHostedService<EmailConsumer>();
            services.AddHostedService<ExportLogConsumer>();

            return services;
        }
    }
}
