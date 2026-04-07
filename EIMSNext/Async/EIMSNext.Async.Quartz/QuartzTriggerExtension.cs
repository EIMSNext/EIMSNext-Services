using EIMSNext.Async.Quartz.Jobs;

using Microsoft.Extensions.Configuration;

using Quartz;

namespace EIMSNext.Async.Quartz
{
    public static class QuartzTriggerExtension
    {
        public static IServiceCollectionQuartzConfigurator AddAsyncQuartzTriggers(this IServiceCollectionQuartzConfigurator qz, IConfiguration configuration)
        {
            var jobKey = new JobKey("DailyTestJob", "Business");
            qz.AddJob<TestJob>(opts => opts
                .WithIdentity(jobKey)
                .StoreDurably()
                .WithDescription("每日业务验证作业"));

            qz.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity("DailyTestTrigger", "Business")
                .WithCronSchedule(
                    configuration["Quartz:TestJob:Cron"] ?? "0 0 0 * * ?",
                    cs => cs.InTimeZone(TimeZoneInfo.Local))
                .WithDescription("每天0点触发测试作业")
                .StartNow());

            return qz;
        }
    }
}
