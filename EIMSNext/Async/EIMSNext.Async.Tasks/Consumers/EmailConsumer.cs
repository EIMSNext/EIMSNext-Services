using EIMSNext.Async.RabbitMQ.Messaging;
using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Common.Extensions;
using Microsoft.Extensions.DependencyInjection;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Entities;

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
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (recipients.Count == 0)
            {
                Logger.LogInformation("Email notify skipped for NotifyId={NotifyId}, no receivers with email", args.NotifyId);
                return;
            }

            var channelId = GetChannelId(args.TaskType);
            var provider = GetProvider(resolver, channelId);
            var baseKey = resolver.Resolve<IOutboxIdempotencyKeyFactory>().Create(args);
            var target = string.Join(',', recipients).ToLowerInvariant();
            var processingRepository = resolver.Resolve<IMessageProcessingRepository>();
            var leaseToken = await processingRepository.TryAcquireAsync(baseKey, target, DateTime.UtcNow.AddMinutes(5), ct);
            if (leaseToken == null)
            {
                Logger.LogInformation("Email notify dedup: key={Key}, target={Target}, skipped", baseKey, target);
                return;
            }

            Logger.LogInformation("Email notify consumed for NotifyId={NotifyId}, Subject={Title}, RecipientCount={RecipientCount}, Channel={Channel}",
                args.NotifyId,
                args.Title,
                recipients.Count,
                channelId);

            try
            {
                await provider.SendAsync(args, recipients, resolver, ct);
                await processingRepository.MarkCompletedAsync(baseKey, target, leaseToken, DateTime.UtcNow.ToTimeStampMs(), ct);
            }
            catch (TaskRequeueException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Email external effect failed; message will be requeued. key={Key}, target={Target}", baseKey, target);
                throw new TaskRequeueException("Email external effect failed.", TimeSpan.FromSeconds(30));
            }
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
