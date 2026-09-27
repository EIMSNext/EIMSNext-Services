using HKH.Mef2.Integration;

namespace EIMSNext.Async.Abstractions.Messaging
{
    public interface IEmailChannelProvider
    {
        Task SendAsync(
            EmailNotifyTaskArgs args,
            IReadOnlyCollection<string> recipients,
            IResolver resolver,
            CancellationToken ct);
    }

    public static class EmailChannelIds
    {
        public const string Default = "default";

        public const string WxWork = "WxWork";
    }
}
