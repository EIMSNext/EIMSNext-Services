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
            var eventFlowJobKey = new JobKey("EventFlowScheduleJob", "EventFlow");
            var outboxDeliveryJobKey = new JobKey("OutboxDeliveryJob", "Outbox");
            var partitionMaintainJobKey = new JobKey("PartitionMaintainJob", "Partition");

            qz.AddJob<PartitionMaintainJob>(opts => opts
                .WithIdentity(partitionMaintainJobKey)
                .StoreDurably()
                .WithDescription("RANGE 分区预建作业（当月+下月）"));

            qz.AddTrigger(opts => opts
                .ForJob(partitionMaintainJobKey)
                .WithIdentity("PartitionMaintainStartupTrigger", "Partition")
                .WithDescription("启动时确保当月/下月分区存在")
                .StartNow());

            qz.AddTrigger(opts => opts
                .ForJob(partitionMaintainJobKey)
                .WithIdentity("PartitionMaintainMonthlyTrigger", "Partition")
                .WithCronSchedule(
                    configuration["Quartz:PartitionMaintainJob:Cron"] ?? "0 0 3 25 * ?",
                    cs => cs.InTimeZone(TimeZoneInfo.Local))
                .WithDescription("每月 25 日凌晨预建下月范围分区（可配置 Quartz:PartitionMaintainJob:Cron）")
                .StartNow());

            qz.AddJob<FormNotifyScheduleJob>(opts => opts
                .WithIdentity(formNotifyJobKey)
                .StoreDurably()
                .WithDescription("表单通知定时扫描作业"));

            qz.AddJob<WfExpireNotifyJob>(opts => opts
                .WithIdentity(wfExpireJobKey)
                .StoreDurably()
                .WithDescription("流程待办超时扫描作业"));

            qz.AddJob<EventFlowScheduleJob>(opts => opts
                .WithIdentity(eventFlowJobKey)
                .StoreDurably()
                .WithDescription("数据流定时触发扫描作业"));

            qz.AddJob<OutboxDeliveryJob>(opts => opts
                .WithIdentity(outboxDeliveryJobKey)
                .StoreDurably()
                .WithDescription("出箱扫描投递作业"));

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
                .ForJob(eventFlowJobKey)
                .WithIdentity("EventFlowScheduleTrigger", "EventFlow")
                .WithCronSchedule(
                    configuration["Quartz:EventFlowScheduleJob:Cron"] ?? "0 0/1 * * * ?",
                    cs => cs.InTimeZone(TimeZoneInfo.Local))
                .WithDescription("每分钟扫描数据流定时调度")
                .StartNow());

            qz.AddTrigger(opts => opts
                .ForJob(outboxDeliveryJobKey)
                .WithIdentity("OutboxDeliveryTrigger", "Outbox")
                .WithCronSchedule(
                    configuration["Quartz:OutboxDeliveryJob:Cron"] ?? "0/10 * * * * ?",
                    cs => cs.InTimeZone(TimeZoneInfo.Local))
                .WithDescription("按 Cron 扫描出箱投递（默认每10秒，可配置 Quartz:OutboxDeliveryJob:Cron）")
                .StartNow());

            return qz;
        }
    }
}
