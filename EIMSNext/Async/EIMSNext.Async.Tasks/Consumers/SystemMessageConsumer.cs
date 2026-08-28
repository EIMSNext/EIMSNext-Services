using EIMSNext.Async.RabbitMQ.Messaging;
using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Entities;
using HKH.Mef2.Integration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EIMSNext.Async.Tasks.Consumers
{
    public class SystemMessageConsumer : TaskConsumerBase<SystemMessageTaskArgs, SystemMessageConsumer>
    {
        public SystemMessageConsumer(IServiceScopeFactory scopeFactory)
            : base(scopeFactory)
        {
        }
        protected override async Task HandleAsync(SystemMessageTaskArgs args, CancellationToken ct, IResolver resolver)
        {
            if (args.Receivers.Count == 0)
            {
                return;
            }

            var repo = resolver.GetRepository<SystemMessage>();
            var processingRepository = resolver.Resolve<IMessageProcessingRepository>();
            var baseKey = resolver.Resolve<IOutboxIdempotencyKeyFactory>().Create(args);
            foreach (var receiver in args.Receivers)
            {
                var target = receiver.EmpId;
                var leaseToken = await processingRepository.TryAcquireAsync(baseKey, target, DateTime.UtcNow.AddMinutes(5), ct);
                if (leaseToken == null)
                {
                    Logger.LogInformation("System message dedup: key={Key}, target={Target}, skipped", baseKey, target);
                    continue;
                }

                try
                {
                    await repo.InsertAsync(new SystemMessage
                    {
                        CorpId = args.CorpId,
                        NotifyId = args.NotifyId,
                        Title = args.Title,
                        Detail = args.Detail,
                        Url = args.Url,
                        ReceiverEmpId = receiver.EmpId,
                        ReceiverName = receiver.EmpName,
                        IsRead = false,
                        ExpireTime = args.ExpireTime,
                        Category = args.Category,
                        CreateTime = DateTime.UtcNow.ToTimeStampMs(),
                        MessageType = args.MessageType,
                    });
                    await processingRepository.MarkCompletedAsync(baseKey, target, leaseToken, DateTime.UtcNow.ToTimeStampMs(), ct);
                }
                catch (TaskRequeueException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "System-message external effect failed; message will be requeued. key={Key}, target={Target}", baseKey, target);
                    throw new TaskRequeueException("System-message external effect failed.", TimeSpan.FromSeconds(30));
                }
            }
        }
    }
}
