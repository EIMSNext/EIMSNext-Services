using EIMSNext.Async.Quartz.Jobs;

using Microsoft.Extensions.DependencyInjection;

namespace EIMSNext.Async.Quartz
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAsyncQuartzJobs(this IServiceCollection services)
        {
            services.AddTransient<TestJob>();
            services.AddTransient<ITestJob>(sp => sp.GetRequiredService<TestJob>());

            return services;
        }
    }
}
