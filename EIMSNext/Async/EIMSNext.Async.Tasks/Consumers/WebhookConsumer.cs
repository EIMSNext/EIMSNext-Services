using System.Text.Json.Nodes;

using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Async.RabbitMQ.Messaging;
using EIMSNext.CloudEvent;
using EIMSNext.Core;
using EIMSNext.Service.Entities;

using HKH.Mef2.Integration;

using Microsoft.Extensions.DependencyInjection;
namespace EIMSNext.Async.Tasks.Consumers
{
    public class WebhookConsumer : TaskConsumerBase<WebhookTaskArgs, WebhookConsumer>
    {
        public WebhookConsumer(IServiceScopeFactory scopeFactory)
            : base(scopeFactory)
        {
        }

        protected override async Task HandleAsync(WebhookTaskArgs args, CancellationToken ct, IResolver resolver)
        {
            if (string.IsNullOrWhiteSpace(args.FormId) || string.IsNullOrWhiteSpace(args.PayloadJson))
            {
                return;
            }

            var webhookRepo = resolver.GetRepository<Webhook>();
            var triggerValue = (long)args.Trigger;
            var webhooks = webhookRepo.Queryable
                .Where(x => x.CorpId == args.CorpId
                    && x.AppId == args.AppId
                    && x.FormId == args.FormId
                    && !x.Disabled
                    && (x.Triggers & triggerValue) == triggerValue)
                .ToList();
            if (webhooks.Count == 0)
            {
                return;
            }

            var payload = JsonNode.Parse(args.PayloadJson) ?? new JsonObject();
            var eventHub = resolver.Resolve<IEventHub>();
            foreach (var webhook in webhooks)
            {
                await eventHub.SendAsync(webhook, args.Trigger, payload);
            }
        }
    }
}
