using EIMSNext.Common;
using EIMSNext.Entities;
using HKH.Common;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

using RestSharp;

namespace EIMSNext.Async.Tasks.System;

public sealed class SystemTokenProvider : ISystemTokenProvider
{
    private const string SecretCacheKey = "identity:system-client-secret";
    private static readonly TimeSpan SecretCacheTtl = TimeSpan.FromMinutes(5);

    private readonly RestClient _client;
    private readonly string _connectionString;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SystemTokenProvider> _logger;

    public SystemTokenProvider(
        AppSetting appSetting,
        IOptions<PostgreSqlOptions> postgreSqlOptions,
        IMemoryCache cache,
        ILogger<SystemTokenProvider> logger)
    {
        var tokenEndpoint = appSetting.IdentityHost.SystemTokenEndPoint
            ?? throw new InvalidOperationException("Missing IdentityHost:BaseUrl or IdentityHost:Authority for system token provider");
        _client = new RestClient(tokenEndpoint);
        _connectionString = postgreSqlOptions.Value.ConnectionString;
        _cache = cache;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync(string corpId, string objectType, string objectId, CancellationToken cancellationToken = default)
    {
        var clientSecret = await GetSystemClientSecretAsync(cancellationToken);

        var request = new RestRequest(string.Empty, Method.Post);
        request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
        request.AddParameter("application/x-www-form-urlencoded",
            $"grant_type={Uri.EscapeDataString(CustomGrantType.System)}&client_id={Uri.EscapeDataString(InternalClients.SystemClientId)}&client_secret={Uri.EscapeDataString(clientSecret)}&scope={Uri.EscapeDataString("api.readwrite")}&corp_id={Uri.EscapeDataString(corpId)}&object_type={Uri.EscapeDataString(objectType)}&object_id={Uri.EscapeDataString(objectId)}",
            ParameterType.RequestBody);

        var response = await _client.ExecuteAsync<SystemTokenResponse>(request, cancellationToken);
        if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Data?.access_token))
        {
            throw new UnLogException(response.ErrorMessage ?? "获取系统 Token 失败");
        }

        return response.Data.access_token;
    }

    // system client 的 secret 明文存 Client 表（轮换走 DB 手动 SQL），进程内缓存 5 分钟：
    // 轮换后最迟 5 分钟生效，无需重启；空值不缓存，secret 缺失仍即时报错。
    // DB 异常细节只进日志，对外抛 UnLogException 通用业务消息。
    private async Task<string> GetSystemClientSecretAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(SecretCacheKey, out string? cached) && !string.IsNullOrEmpty(cached))
        {
            return cached;
        }

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            // Id 是 citext（PK 索引同类型）：参数显式转 citext 保持同类型比较并走 citext 索引，
            // 不做 ::text 列转换（会废掉索引，语义也变成大小写敏感）。
            await using var command = new NpgsqlCommand(
                "SELECT \"ClientSecret\" FROM \"Client\" WHERE \"Id\" = $1::citext AND \"Enabled\"", connection);
            command.Parameters.AddWithValue(InternalClients.SystemClientId);

            var secret = await command.ExecuteScalarAsync(cancellationToken) as string;
            if (string.IsNullOrWhiteSpace(secret))
            {
                throw new UnLogException("Client 表中未找到 eimsnext.system 的 ClientSecret");
            }

            _cache.Set(SecretCacheKey, secret, SecretCacheTtl);
            return secret;
        }
        catch (UnLogException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取 system client secret 失败");
            throw new UnLogException("获取系统客户端密钥失败", ex);
        }
    }

    // RestSharp 只能反序列化公共类型，私有嵌套类会导致所有系统令牌获取恒失败。
    public sealed class SystemTokenResponse
    {
        public string access_token { get; set; } = string.Empty;
    }
}
