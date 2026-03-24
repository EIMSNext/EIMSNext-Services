using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using EIMSNext.Async.Core.Jobs;

using Microsoft.Extensions.Configuration;

using Quartz;

namespace EIMSNext.Async.Core
{
    public static class QuartzTriggerExtension
    {
        public static void AddQuartzTriggers(this IServiceCollectionQuartzConfigurator qz, IConfiguration configuration)
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
                    cs => cs.InTimeZone(TimeZoneInfo.Local)
                )
                .WithDescription("每天0点触发测试作业")
                .StartNow());
        }
    }
}
