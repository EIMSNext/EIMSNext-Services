using System.Composition;

using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Core.Abstractions;

using HKH.Mef2.Integration;

namespace EIMSNext.Async.Tasks.Email;

[Export(typeof(IEmailChannelProvider))]
[ExportMetadata(MefMetadata.Id, EmailChannelIds.Default)]
public sealed class DefaultEmailProvider : IEmailChannelProvider
{
    public Task SendAsync(
        EmailNotifyTaskArgs args,
        IReadOnlyCollection<string> recipients,
        IResolver resolver,
        CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
