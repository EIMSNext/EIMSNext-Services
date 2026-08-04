using System.Security.Claims;
using EIMSNext.ApiCore;
using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Interfaces;
using EIMSNext.Common.Extensions;
using Microsoft.AspNetCore.Http;

namespace EIMSNext.Auth.Services
{
    public abstract class TokenGrantHandlerBase
    {
        private readonly IHttpContextAccessor _contextAccessor;
        private readonly IAuditLoginService _auditLoginService;

        protected TokenGrantHandlerBase(IAuditLoginService auditLoginService, IHttpContextAccessor contextAccessor)
        {
            _auditLoginService = auditLoginService;
            _contextAccessor = contextAccessor;
        }

        protected Task AddAuditLogin(AuditLogin auditLogin)
        {
            return _auditLoginService.AddAuditLogin(auditLogin);
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

        protected AuditLogin CreateFailureAudit(string? loginId, string reason, string grantType = "password")
        {
            return new AuditLogin
            {
                LoginId = loginId,
                ClientId = InternalClients.WebClientId,
                ClientIp = IpHelper.GetClientIp(_contextAccessor),
                CreateTime = DateTime.UtcNow.ToTimeStampMs(),
                GrantType = grantType,
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
                ClientId = InternalClients.WebClientId,
                ClientIp = IpHelper.GetClientIp(_contextAccessor),
                CreateTime = DateTime.UtcNow.ToTimeStampMs(),
                GrantType = grantType
            };
        }

        protected AuditLogin CreateSuccessAudit(string loginId, string? corpId, string clientId, string? userName, string grantType)
        {
            return new AuditLogin
            {
                LoginId = loginId,
                CorpId = corpId,
                ClientId = clientId,
                UserName = userName,
                ClientIp = IpHelper.GetClientIp(_contextAccessor),
                CreateTime = DateTime.UtcNow.ToTimeStampMs(),
                GrantType = grantType
            };
        }
    }
}
