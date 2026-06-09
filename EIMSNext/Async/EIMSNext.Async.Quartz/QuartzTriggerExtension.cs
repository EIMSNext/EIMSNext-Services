using EIMSNext.Async.Quartz.Jobs;

using Microsoft.Extensions.Configuration;

using Quartz;

namespace EIMSNext.Async.Quartz
{
    public static class QuartzTriggerExtension
    {
        public static IServiceCollectionQuartzConfigurator AddAsyncQuartzTriggers(this IServiceCollectionQuartzConfigurator qz, IConfiguration configuration)
        {
            var formNotifyJobKey = new JobKey("FormNotifyScheduleJob", "Notify");
            var wfExpireJobKey = new JobKey("WfExpireNotifyJob", "Notify");
            var dataflowJobKey = new JobKey("DataflowScheduleJob", "Dataflow");
            qz.AddJob<FormNotifyScheduleJob>(opts => opts
                .WithIdentity(formNotifyJobKey)
                .StoreDurably()
                .WithDescription("表单通知定时扫描作业"));

            qz.AddJob<WfExpireNotifyJob>(opts => opts
                .WithIdentity(wfExpireJobKey)
                .StoreDurably()
                .WithDescription("流程待办超时扫描作业"));

            qz.AddJob<DataflowScheduleJob>(opts => opts
                .WithIdentity(dataflowJobKey)
                .StoreDurably()
                .WithDescription("数据流定时触发扫描作业"));

            qz.AddTrigger(opts => opts
                .ForJob(formNotifyJobKey)
                .WithIdentity("FormNotifyScheduleTrigger", "Notify")
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

            qz.AddTrigger(opts => opts
                .ForJob(dataflowJobKey)
                .WithIdentity("DataflowScheduleTrigger", "Dataflow")
                .WithCronSchedule(
                    configuration["Quartz:DataflowScheduleJob:Cron"] ?? "0 0/1 * * * ?",
                    cs => cs.InTimeZone(TimeZoneInfo.Local))
                .WithDescription("每分钟扫描数据流定时调度")
                .StartNow());

            return qz;
        }
    }
}
