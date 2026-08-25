using System.Net.Mime;
using System.Text.Json;
using System.Text.Json.Nodes;
using CloudNative.CloudEvents;
using CloudNative.CloudEvents.Http;
using CloudNative.CloudEvents.SystemTextJson;
using EIMSNext.Common;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Service.Entities;
using Microsoft.Extensions.Logging;

namespace EIMSNext.CloudEvent
{
    /// <summary>
    /// TODO：此类可能最终改为注入类，并放到异步程序中
    /// </summary>
    /// <param name="Logger"></param>
    /// <param name="WebPushLogRepo"></param>
    public class EventHub(ILogger<EventHub> Logger, IRepository<WebPushLog> WebPushLogRepo) : IEventHub
    {
        private static string domain = "eimsnext.com";

        public async Task SendAsync(Webhook webhook, WebHookTrigger trigger, string eventId, object data)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
            var cloudEvent = new CloudNative.CloudEvents.CloudEvent
            {
                Id = eventId,
                Type = $"{webhook.SourceType}.{trigger}".ToLower(),
                Source = new Uri($"http://{domain}/events/{webhook.SourceType}"),
                DataContentType = MediaTypeNames.Application.Json,
                Time = DateTimeOffset.UtcNow,
                Data = FormatData(data).SerializeToJson(),
            };

            var content = cloudEvent.ToHttpContent(ContentMode.Structured, new JsonEventFormatter(null, default));

            using var httpClient = new HttpClient();
            string? result = null;
            int httpCode = 400;
            bool success = false;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, webhook.Url)
                {
                    Content = content
                };
                if (!string.IsNullOrWhiteSpace(webhook.Secret))
                {
                    var payload = await content.ReadAsByteArrayAsync();
                    request.Headers.TryAddWithoutValidation("X-EIMS-Signature", WebhookSignature.Compute(payload, webhook.Secret));
                }

                var resp = await httpClient.SendAsync(request);
                success = resp.IsSuccessStatusCode;
                httpCode = (int)resp.StatusCode;
                result = await resp.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "WebPush失败。WebhookUrl={WebhookUrl}, TriggerType={TriggerType}", webhook.Url, cloudEvent.Type);
            }

            var log = new WebPushLog()
            {
                CorpId = webhook.CorpId,
                AppId = webhook.AppId,
                FormId = webhook.FormId,
                WebHookId = webhook.Id,
                Url = webhook.Url,
                SourceType = webhook.SourceType,
                TriggerType = cloudEvent.Type,
                EventId = cloudEvent.Id,
                PushObject = cloudEvent.Data.ToString(),
                PushResult = success ? "" : result,
                HttpCode = httpCode,
                Success = success
            };

            await WebPushLogRepo.InsertAsync(log);

            if (!success)
            {
                throw new InvalidOperationException($"Webhook delivery failed for event {eventId}.");
            }
        }

        private object FormatData(object data)
        {
            var jsonData = JsonNode.Parse(data.SerializeToJson())!.AsObject();

            jsonData.Remove("corpId");
            jsonData.Remove("updateLog");
            jsonData.Remove("deleteFlag");

            return jsonData;
        }
    }
}
