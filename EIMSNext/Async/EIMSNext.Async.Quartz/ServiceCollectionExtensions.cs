using EIMSNext.Async.Quartz.Jobs;

using Microsoft.Extensions.DependencyInjection;

namespace EIMSNext.Async.Quartz
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAsyncQuartzJobs(this IServiceCollection services)
        {
            services.AddTransient<FormNotifyScheduleJob>();
            services.AddTransient<IFormNotifyScheduleJob>(sp => sp.GetRequiredService<FormNotifyScheduleJob>());
            services.AddTransient<WfExpireNotifyJob>();
            services.AddTransient<IWfExpireNotifyJob>(sp => sp.GetRequiredService<WfExpireNotifyJob>());

            return services;
        }
    }
}
