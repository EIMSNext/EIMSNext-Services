using System.Composition;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Async.RabbitMQ.Messaging;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Services.Extensions;

using HKH.Mef2.Integration;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EIMSNext.Async.Tasks.Email;

[Export(typeof(IEmailChannelProvider))]
[ExportMetadata(MefMetadata.Id, EmailChannelIds.WxWork)]
public sealed class WxWorkEmailChannelProvider : IEmailChannelProvider
{
    private const string CorpId = "ww2b2a7f1847668186";
    private const string AgentId = "1000002";
    private const string Secret = "CAszmY2cGQN3Ve9-Cx0gh-Mhud40wDUA-7tVLDuI2Ic";
    private const string TokenCacheKey = "wxwork:email-token:" + CorpId + ":" + AgentId;
    private static readonly HttpClient SharedHttpClient = new()
    {
        BaseAddress = new Uri("https://qyapi.weixin.qq.com/"),
        Timeout = TimeSpan.FromSeconds(20)
    };

    private readonly HttpClient _httpClient;

    public WxWorkEmailChannelProvider()
        : this(SharedHttpClient)
    {
    }

    internal WxWorkEmailChannelProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task SendAsync(
        EmailNotifyTaskArgs args,
        IReadOnlyCollection<string> recipients,
        IResolver resolver,
        CancellationToken ct)
    {
        var logger = resolver.GetLogger<WxWorkEmailChannelProvider>();
        var cache = resolver.GetMemoryCache();
        var tokenResult = await GetAccessTokenAsync(cache, ct);
        if (tokenResult.AccessToken == null)
        {
            await HandleApiFailureAsync("gettoken", tokenResult.Response, resolver, logger);
            return;
        }

        var response = await ComposeAndSendAsync(tokenResult.AccessToken, args, recipients, ct);
        if (IsTokenInvalid(response.ErrorCode))
        {
            cache.Remove(TokenCacheKey);
            tokenResult = await GetAccessTokenAsync(cache, ct);
            if (tokenResult.AccessToken == null)
            {
                await HandleApiFailureAsync("gettoken", tokenResult.Response, resolver, logger);
                return;
            }

            response = await ComposeAndSendAsync(tokenResult.AccessToken, args, recipients, ct);
        }

        if (response.ErrorCode == 0)
        {
            logger.LogInformation("WxWork email accepted for NotifyId={NotifyId}, TaskType={TaskType}", args.NotifyId, args.TaskType);
            return;
        }

        await HandleApiFailureAsync("compose_send", response, resolver, logger);
    }

    private async Task<TokenAcquisitionResult> GetAccessTokenAsync(IMemoryCache cache, CancellationToken ct)
    {
        if (cache.TryGetValue(TokenCacheKey, out string? cachedToken) && !string.IsNullOrWhiteSpace(cachedToken))
        {
            return new TokenAcquisitionResult(cachedToken, new WxWorkTokenResponse { ErrorCode = 0 });
        }

        WxWorkTokenResponse response;
        try
        {
            var url = $"cgi-bin/gettoken?corpid={Uri.EscapeDataString(CorpId)}&corpsecret={Uri.EscapeDataString(Secret)}";
            using var httpResponse = await _httpClient.GetAsync(url, ct);
            response = await ReadResponseAsync<WxWorkTokenResponse>(httpResponse, ct);
        }
        catch (HttpRequestException)
        {
            throw new TaskRequeueException("WxWork token request failed.", TimeSpan.FromSeconds(30));
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TaskRequeueException("WxWork token request timed out.", TimeSpan.FromSeconds(30));
        }

        if (response.ErrorCode != 0 || string.IsNullOrWhiteSpace(response.AccessToken))
        {
            return new TokenAcquisitionResult(null, response);
        }

        var lifetime = TimeSpan.FromSeconds(Math.Max(60, response.ExpiresIn - 300));
        cache.Set(TokenCacheKey, response.AccessToken, lifetime);
        return new TokenAcquisitionResult(response.AccessToken, response);
    }

    private async Task<WxWorkApiResponse> ComposeAndSendAsync(
        string accessToken,
        EmailNotifyTaskArgs args,
        IReadOnlyCollection<string> recipients,
        CancellationToken ct)
    {
        try
        {
            var payload = new WxWorkMailRequest
            {
                To = new WxWorkRecipients { Emails = recipients.ToList() },
                Subject = args.Title,
                Content = args.Detail,
                ContentType = "html"
            };
            var url = $"cgi-bin/exmail/app/compose_send?access_token={Uri.EscapeDataString(accessToken)}";
            using var response = await _httpClient.PostAsJsonAsync(url, payload, cancellationToken: ct);
            return await ReadResponseAsync<WxWorkApiResponse>(response, ct);
        }
        catch (HttpRequestException)
        {
            throw new TaskRequeueException("WxWork mail request failed.", TimeSpan.FromSeconds(30));
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TaskRequeueException("WxWork mail request timed out.", TimeSpan.FromSeconds(30));
        }
    }

    private static async Task<TResponse> ReadResponseAsync<TResponse>(HttpResponseMessage response, CancellationToken ct)
        where TResponse : WxWorkApiResponse
    {
        var content = await response.Content.ReadAsStringAsync(ct);
        TResponse? result;
        try
        {
            result = JsonSerializer.Deserialize<TResponse>(content);
        }
        catch (JsonException)
        {
            throw new TaskRequeueException(
                $"WxWork returned an invalid JSON response with HTTP status {(int)response.StatusCode} ({response.StatusCode}).",
                TimeSpan.FromSeconds(30));
        }
        if (result?.ErrorCode == null)
        {
            throw new TaskRequeueException(
                $"WxWork returned an invalid response with HTTP status {(int)response.StatusCode} ({response.StatusCode}).",
                TimeSpan.FromSeconds(30));
        }

        return result;
    }

    private static bool IsTokenInvalid(int? errorCode)
    {
        return errorCode is 40014 or 42001;
    }

    private static Task HandleApiFailureAsync(
        string operation,
        WxWorkApiResponse response,
        IResolver resolver,
        ILogger logger)
    {
        var message = $"WxWork {operation} returned errcode={response.ErrorCode}, errmsg={response.ErrorMessage}.";
        if (IsDevelopment(resolver))
        {
            logger.LogWarning("{Message} Development link verification accepted the API response.", message);
            return Task.CompletedTask;
        }

        throw new InvalidOperationException(message);
    }

    private static bool IsDevelopment(IResolver resolver)
    {
        try
        {
            return string.Equals(
                resolver.Resolve<IHostEnvironment>().EnvironmentName,
                Environments.Development,
                StringComparison.OrdinalIgnoreCase);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private class WxWorkApiResponse
    {
        [JsonPropertyName("errcode")]
        public int? ErrorCode { get; set; }

        [JsonPropertyName("errmsg")]
        public string ErrorMessage { get; set; } = string.Empty;
    }

    private sealed class WxWorkTokenResponse : WxWorkApiResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    private sealed record TokenAcquisitionResult(string? AccessToken, WxWorkTokenResponse Response);

    private sealed class WxWorkMailRequest
    {
        [JsonPropertyName("to")]
        public required WxWorkRecipients To { get; init; }

        [JsonPropertyName("subject")]
        public required string Subject { get; init; }

        [JsonPropertyName("content")]
        public required string Content { get; init; }

        [JsonPropertyName("content_type")]
        public required string ContentType { get; init; }
    }

    private sealed class WxWorkRecipients
    {
        [JsonPropertyName("emails")]
        public required List<string> Emails { get; init; }
    }
}
