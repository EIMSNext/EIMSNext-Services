using System.Text.Json;

using EIMSNext.Core;
using EIMSNext.Service.Entities;

using HKH.Mef2.Integration;

namespace EIMSNext.Async.Core.Messaging.Consumers
{
    [Queue("system-message")]
    public class SystemMessageConsumer : TaskConsumerBase<SystemMessageConsumer, SystemMessageTaskArgs>
    {
        public SystemMessageConsumer(IResolver resolver) : base(resolver)
        {
        }

        protected override async Task HandleTaskAsync(string taskType, string argsJson, CancellationToken ct)
        {
            var invoke = JsonSerializer.Deserialize<TaskInvokeArgs<SystemMessageTaskArgs>>(argsJson);
            var args = invoke?.Parameters?.FirstOrDefault();
            if (args == null || args.Receivers.Count == 0)
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
