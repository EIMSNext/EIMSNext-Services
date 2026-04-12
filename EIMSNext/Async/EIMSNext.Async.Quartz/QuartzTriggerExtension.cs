using EIMSNext.Async.Quartz.Jobs;

using Microsoft.Extensions.Configuration;

using Quartz;

namespace EIMSNext.Async.Quartz
{
    public static class QuartzTriggerExtension
    {
        public static IServiceCollectionQuartzConfigurator AddAsyncQuartzTriggers(this IServiceCollectionQuartzConfigurator qz, IConfiguration configuration)
        {
            var formNotifyJobKey = new JobKey("FormNotifyScheduleJob", "Business");
            var wfExpireJobKey = new JobKey("WfExpireNotifyJob", "Business");
            qz.AddJob<FormNotifyScheduleJob>(opts => opts
                .WithIdentity(formNotifyJobKey)
                .StoreDurably()
                .WithDescription("表单通知定时扫描作业"));

            qz.AddJob<WfExpireNotifyJob>(opts => opts
                .WithIdentity(wfExpireJobKey)
                .StoreDurably()
                .WithDescription("流程待办超时扫描作业"));

            qz.AddTrigger(opts => opts
                .ForJob(formNotifyJobKey)
                .WithIdentity("FormNotifyScheduleTrigger", "Business")
                .WithCronSchedule(
                    configuration["Quartz:FormNotifyScheduleJob:Cron"] ?? "0 0/1 * * * ?",
                    cs => cs.InTimeZone(TimeZoneInfo.Local))
                .WithDescription("每分钟触发表单通知定时扫描")
                .StartNow());

            qz.AddTrigger(opts => opts
                .ForJob(wfExpireJobKey)
                .WithIdentity("WfExpireNotifyTrigger", "Notify")
                .WithCronSchedule(
                    configuration["Quartz:WfExpireNotifyJob:Cron"] ?? "0 0/1 * * * ?",
                    cs => cs.InTimeZone(TimeZoneInfo.Local))
                .WithDescription("每分钟触发流程待办超时扫描")
                .StartNow());

            return qz;
        }
    }
}
