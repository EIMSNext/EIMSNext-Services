using EIMSNext.Async.Abstractions.Messaging;
using Microsoft.Extensions.DependencyInjection;
using EIMSNext.Async.RabbitMQ.Messaging;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Service.Entities;

using HKH.Mef2.Integration;

using Microsoft.Extensions.Logging;

using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace EIMSNext.Async.Tasks.Consumers
{
    public class EmailConsumer : TaskConsumerBase<EmailNotifyTaskArgs, EmailConsumer>
    {
        public EmailConsumer(IServiceScopeFactory scopeFactory)
            : base(scopeFactory)
        {
        }

        protected override async Task HandleAsync(EmailNotifyTaskArgs args, CancellationToken ct, IResolver resolver)
        {
            if (args.Receivers.Count == 0)
            {
                return;
            }

            var empRepo = resolver.GetRepository<Employee>();
            var targetEmpIds = args.Receivers
                .Select(x => x.EmpId)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var emails = args.Receivers
                .Select(x => x.Email)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim())
                .ToList();

            if (targetEmpIds.Count > 0)
            {
                emails.AddRange(empRepo.Find(x => targetEmpIds.Contains(x.Id) && !string.IsNullOrEmpty(x.WorkEmail))
                    .ToList()
                    .Select(x => x.WorkEmail!));
            }

            var recipients = emails
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (recipients.Count == 0)
            {
                Logger.LogInformation("Email notify skipped for NotifyId={NotifyId}, no receivers with email", args.NotifyId);
                return;
            }

            var channelId = GetChannelId(args.TaskType);
            var provider = GetProvider(resolver, channelId);

            Logger.LogInformation("Email notify consumed for NotifyId={NotifyId}, Subject={Title}, RecipientCount={RecipientCount}, Channel={Channel}",
                args.NotifyId,
                args.Title,
                recipients.Count,
                channelId);

            await provider.SendAsync(args, recipients, resolver, ct);
        }

        private IEmailChannelProvider GetProvider(IResolver resolver, string channelId)
        {
            try
            {
                return resolver.ResolveExport<IEmailChannelProvider>(channelId);
            }
            catch (NotSupportedException) when (!string.Equals(channelId, EmailChannelIds.Default, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogWarning("Email channel {Channel} is unavailable; falling back to {DefaultChannel}", channelId, EmailChannelIds.Default);
                return resolver.ResolveExport<IEmailChannelProvider>(EmailChannelIds.Default);
            }
        }

        private static string GetChannelId(EmailTaskType taskType)
        {
            return taskType == EmailTaskType.PlatWork
                ? EmailChannelIds.WxWork
                : EmailChannelIds.Default;
        }
    }
}
