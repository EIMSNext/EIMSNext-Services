using System.Security.Claims;
using EIMSNext.ApiService;
using EIMSNext.Entities;
using EIMSNext.Identity.Interfaces;
using EIMSNext.Identity.Models;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;

namespace EIMSNext.Identity.Services;

public sealed class SystemTokenGrantHandler : TokenGrantHandlerBase, ITokenGrantHandler
{
    public SystemTokenGrantHandler(IIdentityLoginAuditService auditLoginService, IHttpContextAccessor contextAccessor)
        : base(auditLoginService, contextAccessor)
    {
    }

    public string GrantType => CustomGrantType.System;

    public Task<TokenRequestResult> HandleAsync(Client client, OpenIddictRequest request, IReadOnlyList<string> scopes, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(client.Id, InternalClients.SystemClientId, StringComparison.Ordinal))
        {
            return Task.FromResult(TokenRequestResult.Failure(OpenIddictConstants.Errors.UnauthorizedClient, "The client application is not allowed to use this grant type."));
        }

        var corpId = request.GetParameter("corp_id")?.ToString() ?? string.Empty;
        var objectType = request.GetParameter("object_type")?.ToString() ?? string.Empty;
        var objectId = request.GetParameter("object_id")?.ToString() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(corpId) || string.IsNullOrWhiteSpace(objectType) || string.IsNullOrWhiteSpace(objectId))
        {
            return Task.FromResult(TokenRequestResult.Failure(OpenIddictConstants.Errors.InvalidRequest, "corp_id、object_type 和 object_id 不能为空"));
        }

        var name = $"{objectType}_{objectId}";
        var authenticationTime = DateTimeOffset.UtcNow;
        var claims = new List<Claim>
        {
            new(IdentityClaimTypes.Subject, "system"),
            new(IdentityClaimTypes.Name, name),
            new(IdentityClaimTypes.Id, "system"),
            new(IdentityClaimTypes.Corp, corpId),
            new(IdentityClaimTypes.IdentityType, IdentityType.System.ToString()),
            new(IdentityClaimTypes.AuthTime, authenticationTime.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        return Task.FromResult(TokenRequestResult.Success("system", CustomGrantType.System, client.AccessTokenLifetime, scopes, claims));
    }
}
