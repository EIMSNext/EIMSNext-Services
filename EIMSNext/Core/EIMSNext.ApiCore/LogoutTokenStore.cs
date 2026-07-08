using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace EIMSNext.ApiCore
{
    public interface ILogoutTokenStore
    {
        Task MarkLoggedOutAsync(string token, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default);

        Task<bool> IsLoggedOutAsync(string token, CancellationToken cancellationToken = default);
    }

    public sealed class DistributedLogoutTokenStore(IDistributedCache cache) : ILogoutTokenStore
    {
        private const string Marker = "logout";

        public async Task MarkLoggedOutAsync(string token, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(token) || expiresAtUtc <= DateTimeOffset.UtcNow)
            {
                return;
            }

            await cache.SetStringAsync(
                LogoutTokenHelper.GetCacheKey(token),
                Marker,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpiration = expiresAtUtc
                },
                cancellationToken);
        }

        public async Task<bool> IsLoggedOutAsync(string token, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            return await cache.GetStringAsync(LogoutTokenHelper.GetCacheKey(token), cancellationToken) is not null;
        }
    }

    public static class LogoutTokenHelper
    {
        private const string CacheKeyPrefix = "auth:logout:";
        private const string BearerPrefix = "Bearer ";

        public static string GetCacheKey(string token)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(token);

            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return CacheKeyPrefix + Convert.ToHexString(hash);
        }

        public static string? ReadBearerToken(HttpRequest request)
        {
            if (!request.Headers.TryGetValue("Authorization", out var authorizationHeader))
            {
                return null;
            }

            var authorization = authorizationHeader.ToString();
            if (!authorization.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var token = authorization[BearerPrefix.Length..].Trim();
            return string.IsNullOrWhiteSpace(token) ? null : token;
        }

        public static DateTimeOffset? ReadExpirationUtc(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            try
            {
                var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
                if (!jwt.Payload.Expiration.HasValue)
                {
                    return null;
                }

                return DateTimeOffset.FromUnixTimeSeconds(jwt.Payload.Expiration.Value);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }

    public static class JwtBearerLogoutTokenEvents
    {
        public static JwtBearerEvents Create()
        {
            return new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var token = LogoutTokenHelper.ReadBearerToken(context.Request);
                    if (string.IsNullOrWhiteSpace(token))
                    {
                        return;
                    }

                    var logoutTokenStore = context.HttpContext.RequestServices.GetRequiredService<ILogoutTokenStore>();
                    if (await logoutTokenStore.IsLoggedOutAsync(token, context.HttpContext.RequestAborted))
                    {
                        context.Fail("Token has been logged out.");
                    }
                }
            };
        }
    }
}
