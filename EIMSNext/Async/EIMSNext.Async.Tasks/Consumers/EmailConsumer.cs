using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Async.RabbitMQ.Messaging;
using EIMSNext.Core;
using EIMSNext.Core.Repositories;
using EIMSNext.Service.Entities;

using HKH.Mef2.Integration;

using Microsoft.Extensions.Logging;

using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace EIMSNext.Async.Tasks.Consumers
{
    public class EmailConsumer : TaskConsumerBase<EmailNotifyTaskArgs, EmailConsumer>
    {
        public EmailConsumer(IResolver resolver)
            : base(resolver)
        {
        }

        protected override Task HandleAsync(EmailNotifyTaskArgs args, CancellationToken ct)
        {
            if (args.Receivers.Count == 0)
            {
                return Task.CompletedTask;
            }

            var empRepo = Resolver.GetRepository<Employee>();
            var targetEmpIds = args.Receivers.Select(x => x.EmpId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var employees = empRepo.Find(x => targetEmpIds.Contains(x.Id) && !string.IsNullOrEmpty(x.WorkEmail))
                .ToList();
            var emails = employees
                .Select(x => x.WorkEmail!)
                .ToList();

            if (emails.Count == 0)
            {
                Logger.LogInformation("Email notify skipped for NotifyId={NotifyId}, no receivers with email", args.NotifyId);
                return Task.CompletedTask;
            }

            Logger.LogInformation("Email notify queued for NotifyId={NotifyId}, Subject={Title}, To={Receivers}, Url={Url}",
                args.NotifyId,
                args.Title,
                string.Join(',', emails.Distinct(StringComparer.OrdinalIgnoreCase)),
                args.Url);

            return Task.CompletedTask;
        }
    }
}
