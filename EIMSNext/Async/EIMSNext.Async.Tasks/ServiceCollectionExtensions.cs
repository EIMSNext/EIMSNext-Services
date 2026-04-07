using EIMSNext.Async.Tasks.Consumers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EIMSNext.Async.Tasks
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAsyncTaskConsumers(this IServiceCollection services)
        {
            services.AddSingleton<IHostedService, FormNotifyDispatchConsumer>();
            services.AddSingleton<IHostedService, SystemMessageConsumer>();
            services.AddSingleton<IHostedService, EmailConsumer>();

            return services;
        }
    }
}
