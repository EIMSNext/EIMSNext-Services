using System.Security.Claims;
using EIMSNext.ApiCore;
using EIMSNext.Auth.Entities;
using EIMSNext.Common.Extensions;
using Microsoft.AspNetCore.Http;

namespace EIMSNext.Auth.Services
{
    public abstract class TokenGrantHandlerBase
    {
        private readonly IHttpContextAccessor _contextAccessor;

        protected TokenGrantHandlerBase(IHttpContextAccessor contextAccessor)
        {
            _contextAccessor = contextAccessor;
        }

        protected static List<Claim> CreateUserClaims(string subject, User user, DateTimeOffset authenticationTime)
        {
            var corp = user.Crops.FirstOrDefault(x => x.IsDefault);
            if (corp == null || string.IsNullOrEmpty(corp.CorpId))
            {
                corp = user.Crops.FirstOrDefault(x => x.IsCorpOwner);
            }

            return new List<Claim>
            {
                new(AuthClaimTypes.Subject, subject),
                new(AuthClaimTypes.Name, user.Name),
                new(AuthClaimTypes.Id, user.Id),
                new(AuthClaimTypes.Corp, corp?.CorpId ?? string.Empty),
                new(AuthClaimTypes.AuthTime, authenticationTime.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };
        }

        protected AuditLogin CreateFailureAudit(string? loginId, string reason)
        {
            return new AuditLogin
            {
                LoginId = loginId,
                ClientId = Constants.ClientId_Web,
                ClientIp = IpHelper.GetClientIp(_contextAccessor),
                CreateTime = DateTime.UtcNow.ToTimeStampMs(),
                GrantType = "password",
                FailReason = reason
            };
        }

        protected AuditLogin CreateSuccessAudit(string loginId, User user, IReadOnlyCollection<Claim> claims, string grantType)
        {
            return new AuditLogin
            {
                LoginId = loginId,
                UserId = user.Id,
                UserName = user.Name,
                CorpId = claims.FirstOrDefault(x => x.Type == AuthClaimTypes.Corp)?.Value,
                ClientId = Constants.ClientId_Web,
                ClientIp = IpHelper.GetClientIp(_contextAccessor),
                CreateTime = DateTime.UtcNow.ToTimeStampMs(),
                GrantType = grantType
            };
        }
    }
}
