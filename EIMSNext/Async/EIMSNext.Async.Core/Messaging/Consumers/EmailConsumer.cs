using HKH.Mef2.Integration;

using EIMSNext.Core;
using EIMSNext.Service.Entities;

using System.Text.Json;

using Microsoft.Extensions.Logging;

using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace EIMSNext.Async.Core.Messaging.Consumers
{
    [Queue("email")] 
    public class EmailConsumer : TaskConsumerBase<EmailConsumer, EmailNotifyTaskArgs>
    {
        public EmailConsumer(IResolver resolver)
            : base(resolver)
        { }

        protected override async Task HandleTaskAsync(string taskType, string argsJson, CancellationToken ct)
        {
            var invoke = JsonSerializer.Deserialize<TaskInvokeArgs<EmailNotifyTaskArgs>>(argsJson);
            var args = invoke?.Parameters?.FirstOrDefault();
            if (args == null || args.Receivers.Count == 0)
            {
                return;
            }

            var empRepo = Resolver.GetRepository<Employee>();
            var targetEmpIds = args.Receivers.Select(x => x.EmpId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var emails = await empRepo.Find(x => targetEmpIds.Contains(x.Id) && !string.IsNullOrEmpty(x.WorkEmail))
                .Project(x => x.WorkEmail)
                .ToListAsync(ct);

            if (emails.Count == 0)
            {
                Logger.LogInformation("Email notify skipped for NotifyId={NotifyId}, no receivers with email", args.NotifyId);
                return;
            }

            Logger.LogInformation("Email notify queued for NotifyId={NotifyId}, Subject={Title}, To={Receivers}, Url={Url}",
                args.NotifyId,
                args.Title,
                string.Join(',', emails.Distinct(StringComparer.OrdinalIgnoreCase)),
                args.Url);

            await Task.CompletedTask;
        }
    }
}
