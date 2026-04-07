using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Async.RabbitMQ.Messaging;
using EIMSNext.Core;
using EIMSNext.Core.Repositories;
using EIMSNext.Service.Entities;

using HKH.Mef2.Integration;

namespace EIMSNext.Async.Tasks.Consumers
{
    public class SystemMessageConsumer : TaskConsumerBase<SystemMessageTaskArgs, SystemMessageConsumer>
    {
        public SystemMessageConsumer(IResolver resolver)
            : base(resolver)
        {
        }

        protected override async Task HandleAsync(SystemMessageTaskArgs args, CancellationToken ct)
        {
            if (args.Receivers.Count == 0)
            {
                return;
            }

            var repo = Resolver.GetRepository<SystemMessage>();
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
                Category = args.Category
            }).ToList();

            await repo.InsertAsync(messages);
        }
    }
}
