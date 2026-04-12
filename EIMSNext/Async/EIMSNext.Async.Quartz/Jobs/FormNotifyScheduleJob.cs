using EIMSNext.Service.Entities;
using EIMSNext.Core;
using HKH.Mef2.Integration;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Quartz;

namespace EIMSNext.Async.Quartz.Jobs
{
    public class FormNotifyScheduleJob : JobBase<FormNotifyScheduleJob>, IFormNotifyScheduleJob
    {
        public FormNotifyScheduleJob(IResolver resolver) : base(resolver)
        {
        }

        protected override Task ExecuteAsync(IJobExecutionContext context)
        {
            var notifyRepo = Resolver.GetRepository<FormNotify>();
            var scheduled = notifyRepo.Find(x =>
                !x.Disabled &&
                (x.TriggerMode == FormNotifyTriggerMode.CustomScheduled || x.TriggerMode == FormNotifyTriggerMode.TimeFieldScheduled))
                .ToList();

            Logger.LogInformation("Form notify schedule scan found {Count} scheduled notifies", scheduled.Count);
            return Task.CompletedTask;
        }
    }
}
