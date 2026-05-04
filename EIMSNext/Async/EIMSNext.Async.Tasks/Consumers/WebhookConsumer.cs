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
            var webhookAliasRepo = resolver.GetRepository<WebhookAlias>();
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
            var aliasConfig = webhookAliasRepo.Queryable
                .FirstOrDefault(x => x.CorpId == args.CorpId
                    && x.AppId == args.AppId
                    && x.FormId == args.FormId);
            var eventHub = resolver.Resolve<IEventHub>();
            foreach (var webhook in webhooks)
            {
                var webhookPayload = payload.DeepClone();
                ApplyAliases(webhookPayload, aliasConfig?.FieldAlias ?? []);
                await eventHub.SendAsync(webhook, args.Trigger, webhookPayload);
            }
        }

        private static void ApplyAliases(JsonNode payload, List<FieldAliasItem> aliases)
        {
            if (aliases.Count == 0 || payload is not JsonObject root)
            {
                return;
            }

            if (root["data"] is JsonObject dataNode)
            {
                ApplyAliasItems(dataNode, aliases);
            }
        }

        private static void ApplyAliasItems(JsonObject target, List<FieldAliasItem> aliases)
        {
            foreach (var item in aliases)
            {
                if (string.IsNullOrWhiteSpace(item.Field) || !target.TryGetPropertyValue(item.Field, out var value))
                {
                    continue;
                }

                if (value is JsonArray array && item.Children?.Count > 0)
                {
                    foreach (var row in array.OfType<JsonObject>())
                    {
                        ApplyAliasItems(row, item.Children);
                    }
                }

                if (!string.IsNullOrWhiteSpace(item.Alias) && item.Alias != item.Field)
                {
                    target.Remove(item.Field);
                    target[item.Alias] = value;
                }
            }
        }
    }
}
