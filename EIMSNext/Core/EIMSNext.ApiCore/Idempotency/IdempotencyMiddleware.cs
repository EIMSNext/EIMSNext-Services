using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using EIMSNext.Cache;

namespace EIMSNext.ApiCore.Idempotency;

public sealed class IdempotencyMiddleware(RequestDelegate next, ICacheClient cache, IOptions<IdempotencyOptions> options, ILogger<IdempotencyMiddleware> logger)
{
    private readonly IdempotencyOptions _options = options.Value;
    private readonly ICacheClient _cache = cache;
    private static readonly HashSet<string> Methods = ["POST", "PUT", "PATCH", "DELETE"];

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.Enabled || !Methods.Contains(context.Request.Method) || IsExcluded(context.Request.Path)) { await next(context); return; }
        var key = context.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(key)) { if (_options.RequireKey) { context.Response.StatusCode = 400; await context.Response.WriteAsJsonAsync(new { error = "Idempotency-Key is required." }); return; } await next(context); return; }
        if (key.Length > 200) { context.Response.StatusCode = 400; await context.Response.WriteAsJsonAsync(new { error = "Invalid Idempotency-Key." }); return; }

        context.Request.EnableBuffering();
        using var body = new MemoryStream(); await context.Request.Body.CopyToAsync(body); context.Request.Body.Position = 0;
        var hash = Convert.ToHexString(SHA256.HashData(body.ToArray()));
        var identity = context.User.Identity?.IsAuthenticated == true ? (context.User.FindFirst("sub")?.Value ?? context.User.Identity.Name ?? "user") : null;
        var authorization = context.Request.Headers.Authorization.ToString();
        var scope = identity ?? (string.IsNullOrWhiteSpace(authorization) ? (context.Connection.RemoteIpAddress?.ToString() ?? "anonymous") : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(authorization))));
        var redisKey = $"IDEMPOTENCY:{scope}:{context.Request.Method}:{context.Request.Path}:{key}";
        string? existing;
        try { existing = await _cache.GetStringAsync(redisKey, CacheScope.Global); }
        catch (Exception ex) { logger.LogWarning(ex, "Idempotency store unavailable"); if (_options.FailOpenWhenStoreUnavailable) { await next(context); return; } throw; }
        if (!string.IsNullOrEmpty(existing))
        {
            var record = JsonSerializer.Deserialize<Record>(existing);
            if (record?.Hash != hash) { context.Response.StatusCode = 409; await context.Response.WriteAsJsonAsync(new { error = "Idempotency-Key was already used with a different request." }); return; }
            if (record.State == "Processing") { context.Response.StatusCode = 409; await context.Response.WriteAsJsonAsync(new { error = "Request is already in progress." }); return; }
            context.Response.StatusCode = record.Status; if (!string.IsNullOrEmpty(record.ContentType)) context.Response.ContentType = record.ContentType; await context.Response.Body.WriteAsync(Convert.FromBase64String(record.Body ?? "")); return;
        }
        var processing = JsonSerializer.Serialize(new Record("Processing", hash, 0, null, null));
        if (!await _cache.TrySetStringAsync(redisKey, processing, TimeSpan.FromSeconds(_options.ProcessingTimeoutSeconds), CacheScope.Global)) { context.Response.StatusCode = 409; await context.Response.WriteAsJsonAsync(new { error = "Request is already in progress." }); return; }
        var original = context.Response.Body; await using var buffer = new MemoryStream(); context.Response.Body = buffer;
        try { await next(context); if (buffer.Length <= _options.MaxResponseBodyBytes) { var completed = JsonSerializer.Serialize(new Record("Completed", hash, context.Response.StatusCode, context.Response.ContentType, Convert.ToBase64String(buffer.ToArray()))); await _cache.SetStringAsync(redisKey, completed, CacheScope.Global, options: new Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.TtlMinutes) }); } buffer.Position = 0; await buffer.CopyToAsync(original); }
        catch { throw; }
        finally { context.Response.Body = original; }
    }
    private bool IsExcluded(PathString path) => _options.ExcludedPathPrefixes.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));
    private sealed record Record(string State, string Hash, int Status, string? ContentType, string? Body);
}

