using System.Globalization;
using System.Security.Claims;
using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace EIMSNext.Auth.Host.Controllers
{
    [ApiController]
    public class AuthorizationController : ControllerBase
    {
        private readonly ITokenRequestHandler _tokenRequestHandler;

        public AuthorizationController(ITokenRequestHandler tokenRequestHandler)
        {
            _tokenRequestHandler = tokenRequestHandler;
        }

        [HttpPost("~/connect/token")]
        [Consumes("application/x-www-form-urlencoded")]
        [Produces("application/json")]
        public async Task<IActionResult> Exchange(CancellationToken cancellationToken)
        {
            var request = HttpContext.GetOpenIddictServerRequest();
            if (request == null)
            {
                return BadRequest(new OpenIddictResponse
                {
                    Error = Errors.InvalidRequest,
                    ErrorDescription = "The OpenID Connect request cannot be retrieved."
                });
            }

            var result = await _tokenRequestHandler.HandleAsync(request, cancellationToken);
            if (!result.Succeeded)
            {
                return Forbid(
                    new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = result.Error,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = result.ErrorDescription
                    }),
                    [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
            }

            var identity = new ClaimsIdentity(TokenValidationParameters.DefaultAuthenticationType, AuthClaimTypes.Name, ClaimTypes.Role);
            foreach (var claim in result.Claims)
            {
                identity.AddClaim(claim);
            }

            identity.AddClaim(new Claim(AuthClaimTypes.ClientId, request.ClientId ?? string.Empty));

            var principal = new ClaimsPrincipal(identity);
            var expiresAt = DateTimeOffset.UtcNow.AddSeconds(result.AccessTokenLifetime);

            principal.SetScopes(result.Scopes);
            principal.SetAudiences("eimsnext.api");
            principal.SetCreationDate(DateTimeOffset.UtcNow);
            principal.SetExpirationDate(expiresAt);
            principal.SetAccessTokenLifetime(TimeSpan.FromSeconds(result.AccessTokenLifetime));

            principal.SetDestinations(static claim => claim.Type switch
            {
                AuthClaimTypes.Id or AuthClaimTypes.Corp or AuthClaimTypes.ClientId or AuthClaimTypes.Subject
                    => [Destinations.AccessToken],
                AuthClaimTypes.Name => [Destinations.AccessToken],
                _ => [Destinations.AccessToken]
            });

            return SignIn(
                principal,
                new AuthenticationProperties(new Dictionary<string, string?>
                {
                    ["access_token_lifetime"] = result.AccessTokenLifetime.ToString(CultureInfo.InvariantCulture)
                }),
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }
    }
}
