using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Async.RabbitMQ.Messaging;
using EIMSNext.Common.Extensions;
using EIMSNext.Core;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;
using Microsoft.Extensions.DependencyInjection;

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
            var messages = args.Receivers.Select(x => new SystemMessage
            {
                CorpId = args.CorpId,
                NotifyId = args.NotifyId,
                Title = args.Title,
                Detail = args.Detail,
                Url = args.Url,
                ReceiverEmpId = x.EmpId,
                ReceiverName = x.EmpName,
                IsRead = false,
                ExpireTime = args.ExpireTime,
                Category = args.Category,
                CreateTime = DateTime.UtcNow.ToTimeStampMs(),
                MessageType = args.MessageType,
            }).ToList();

            await repo.InsertAsync(messages);
        }
    }
}
