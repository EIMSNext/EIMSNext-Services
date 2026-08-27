using System.Security.Claims;
using EIMSNext.ApiCore;
using EIMSNext.Entities;
using EIMSNext.Identity.Interfaces;
using EIMSNext.Common.Extensions;
using Microsoft.AspNetCore.Http;

namespace EIMSNext.Identity.Services
{
    public abstract class TokenGrantHandlerBase
    {
        private readonly IHttpContextAccessor _contextAccessor;
        private readonly IIdentityLoginAuditService _auditLoginService;

        protected TokenGrantHandlerBase(IIdentityLoginAuditService auditLoginService, IHttpContextAccessor contextAccessor)
        {
            _auditLoginService = auditLoginService;
            _contextAccessor = contextAccessor;
        }

        protected Task AddIdentityLoginAudit(IdentityLoginAudit auditLogin)
        {
            return _auditLoginService.AddIdentityLoginAudit(auditLogin);
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
                new(IdentityClaimTypes.Subject, subject),
                new(IdentityClaimTypes.Name, user.Name),
                new(IdentityClaimTypes.Id, user.Id),
                new(IdentityClaimTypes.Corp, corp?.CorpId ?? string.Empty),
                new(IdentityClaimTypes.AuthTime, authenticationTime.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };
        }

        protected IdentityLoginAudit CreateFailureAudit(string? loginId, string reason, string grantType = "password")
        {
            return new IdentityLoginAudit
            {
                LoginId = loginId,
                ClientId = InternalClients.WebClientId,
                ClientIp = IpHelper.GetClientIp(_contextAccessor),
                CreateTime = DateTime.UtcNow.ToTimeStampMs(),
                GrantType = grantType,
                FailReason = reason
            };
        }

        protected IdentityLoginAudit CreateSuccessAudit(string loginId, User user, IReadOnlyCollection<Claim> claims, string grantType)
        {
            return new IdentityLoginAudit
            {
                LoginId = loginId,
                UserId = user.Id,
                UserName = user.Name,
                CorpId = claims.FirstOrDefault(x => x.Type == IdentityClaimTypes.Corp)?.Value,
                ClientId = InternalClients.WebClientId,
                ClientIp = IpHelper.GetClientIp(_contextAccessor),
                CreateTime = DateTime.UtcNow.ToTimeStampMs(),
                GrantType = grantType
            };
        }

        protected IdentityLoginAudit CreateSuccessAudit(string loginId, string? corpId, string clientId, string? userName, string grantType)
        {
            return new IdentityLoginAudit
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
