using System.Net.Mime;
using System.Text.Json;
using CloudNative.CloudEvents;
using CloudNative.CloudEvents.Http;
using CloudNative.CloudEvents.SystemTextJson;

namespace EIMSNext.CloudEvent
{
    public static class EventHub
    {
        public static async Task SendAsync(CloudEventArgs args)
        {

            var cloudEvent = new CloudNative.CloudEvents.CloudEvent
            {
                Id = Guid.NewGuid().ToString(),
                Type = $"{args.EventSource}.{args.EventType}".ToLower(),
                Source = new Uri($"http://cc.com/cloudevents/{args.FormId ?? args.EventSource.ToLower()}"),
                DataContentType = MediaTypeNames.Application.Json,
                Time = DateTimeOffset.UtcNow,
                Data = JsonSerializer.Serialize(args.Data),
            };

            var content = cloudEvent.ToHttpContent(ContentMode.Structured, new JsonEventFormatter(null, default));

            var httpClient = new HttpClient();
            var result = await httpClient.PostAsync(args.Url, content);
        }
    }
    public class CloudEventArgs
    {
        public required string Url { get; set; }
        public CloudEventType EventType { get; set; }
        public required string EventSource { get; set; }
        public string? FormId { get; set; }
        public required CloudEventData Data { get; set; }
    }
    public enum CloudEventType
    {
        Create,
        Modify,
        Delete,
    }
    public static class CloudEventSource
    {
        public const string Test = "test";
    }
    public class CloudEventData
    {
        public object? OriValue { get; set; }
        public object? Value { get; set; }
    }
}
