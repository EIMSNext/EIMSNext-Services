using System.Net.Mime;
using System.Text.Json;
using System.Text.Json.Nodes;
using CloudNative.CloudEvents;
using CloudNative.CloudEvents.Http;
using CloudNative.CloudEvents.SystemTextJson;
using EIMSNext.Common;
using EIMSNext.Core.Repositories;
using EIMSNext.Service.Entities;
using Microsoft.Extensions.Logging;

namespace EIMSNext.CloudEvent
{
    /// <summary>
    /// TODO：此类可能最终改为注入类，并放到异步程序中
    /// </summary>
    /// <param name="Logger"></param>
    /// <param name="Appsetting"></param>
    /// <param name="WebPushLogRepo"></param>
    /// <param name="WebHookRepo"></param>
    public class EventHub(ILogger<EventHub> Logger, AppSetting Appsetting, IRepository<Webhook> WebHookRepo, IRepository<WebPushLog> WebPushLogRepo) : IEventHub
    {
        private static string domain = "eimsnext.com";

        public async Task SendAsync(Webhook webhook, WebHookTrigger trigger, object data)
        {
            var cloudEvent = new CloudNative.CloudEvents.CloudEvent
            {
                Id = Guid.NewGuid().ToString(),
                Type = $"{webhook.SourceType}.{trigger}".ToLower(),
                Source = new Uri($"http://{domain}/events/{webhook.SourceType}"),
                DataContentType = MediaTypeNames.Application.Json,
                Time = DateTimeOffset.UtcNow,
                Data = FormatData(data).SerializeToJson(),
            };

            var content = cloudEvent.ToHttpContent(ContentMode.Structured, new JsonEventFormatter(null, default));

            var httpClient = new HttpClient();
            string? result = null;
            int httpCode = 400;
            bool success = false;
            try
            {
                var resp = await httpClient.PostAsync(webhook.Url, content);
                success = resp.IsSuccessStatusCode;
                httpCode = (int)resp.StatusCode;
                result = await resp.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"WebPush失败: ");
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
