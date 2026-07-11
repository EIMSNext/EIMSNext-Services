using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Interfaces;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using static OpenIddict.Abstractions.OpenIddictConstants;

namespace EIMSNext.Auth.Host.Controllers
{
    [ApiController]
    public class AuthorizationController : ControllerBase
    {
        private readonly ITokenRequestHandler _tokenRequestHandler;
        private readonly IBuiltInClientRequestPolicy _builtInClientRequestPolicy;

        public AuthorizationController(
            ITokenRequestHandler tokenRequestHandler,
            IBuiltInClientRequestPolicy builtInClientRequestPolicy)
        {
            _tokenRequestHandler = tokenRequestHandler;
            _builtInClientRequestPolicy = builtInClientRequestPolicy;
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

            var validation = _builtInClientRequestPolicy.ValidateTokenEndpoint(request.ClientId);
            if (!validation.Succeeded)
            {
                return CreateErrorResult(validation.Error!, validation.ErrorDescription!);
            }

            return await HandleTokenRequestAsync(request, cancellationToken);
        }

        [Route("~/auth/login"), HttpPost]
        [Consumes("application/x-www-form-urlencoded")]
        [Produces("application/json")]
        public async Task<IActionResult> Login([FromForm] EncryptedLoginRequest body, CancellationToken cancellationToken)
        {
            var fieldsResult = ParseEncryptedFields(body);
            if (fieldsResult.Result != null)
            {
                return fieldsResult.Result;
            }

            var fields = fieldsResult.Fields!;
            if (!fields.TryGetValue("username", out var username) || string.IsNullOrWhiteSpace(username) ||
                !fields.TryGetValue("password", out var password))
            {
                return BadRequest(new OpenIddictResponse
                {
                    Error = Errors.InvalidRequest,
                    ErrorDescription = "The username and password fields are required."
                });
            }

            var clientId = fields.TryGetValue("client_id", out var clientIdValue) ? clientIdValue : null;
            var validation = _builtInClientRequestPolicy.ValidateLogin(clientId, Request);
            if (!validation.Succeeded)
            {
                return CreateErrorResult(validation.Error!, validation.ErrorDescription!);
            }

            var request = new OpenIddictRequest
            {
                GrantType = fields.TryGetValue("grant_type", out var grantType) && !string.IsNullOrWhiteSpace(grantType) ? grantType : "password",
                Username = username,
                Password = password,
                ClientId = clientId,
                Scope = fields.TryGetValue("scope", out var scope) ? scope : null,
            };

            return await HandleTokenRequestAsync(request, cancellationToken);
        }

        [Route("~/public/token"), HttpPost]
        [Consumes("application/x-www-form-urlencoded")]
        [Produces("application/json")]
        public async Task<IActionResult> PublicToken([FromForm] EncryptedLoginRequest body, CancellationToken cancellationToken)
        {
            var fieldsResult = ParseEncryptedFields(body);
            if (fieldsResult.Result != null)
            {
                return fieldsResult.Result;
            }

            var fields = fieldsResult.Fields!;
            if (!fields.TryGetValue("username", out var username) || string.IsNullOrWhiteSpace(username) ||
                !fields.TryGetValue("password", out var password))
            {
                return BadRequest(new OpenIddictResponse
                {
                    Error = Errors.InvalidRequest,
                    ErrorDescription = "The username and password fields are required."
                });
            }

            var clientId = fields.TryGetValue("client_id", out var clientIdValue) ? clientIdValue : null;
            var grantType = fields.TryGetValue("grant_type", out var grantTypeValue) ? grantTypeValue : null;
            var validation = _builtInClientRequestPolicy.ValidatePublicToken(clientId, grantType, Request);
            if (!validation.Succeeded)
            {
                return CreateErrorResult(validation.Error!, validation.ErrorDescription!);
            }

            var request = new OpenIddictRequest
            {
                GrantType = CustomGrantType.Public,
                Username = username,
                Password = password,
                ClientId = clientId,
                Scope = fields.TryGetValue("scope", out var scope) ? scope : null,
            };

            return await HandleTokenRequestAsync(request, cancellationToken);
        }

        [HttpPost("~/system/token")]
        [Consumes("application/x-www-form-urlencoded")]
        [Produces("application/json")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> SystemToken(CancellationToken cancellationToken)
        {
            if (!Request.HasFormContentType)
            {
                return BadRequest(new OpenIddictResponse
                {
                    Error = Errors.InvalidRequest,
                    ErrorDescription = "The request must use application/x-www-form-urlencoded."
                });
            }

            var fields = await Request.ReadFormAsync(cancellationToken);
            var clientId = fields["client_id"].ToString();
            var grantType = fields["grant_type"].ToString();
            var validation = _builtInClientRequestPolicy.ValidateSystemToken(clientId, grantType);
            if (!validation.Succeeded)
            {
                return CreateErrorResult(validation.Error!, validation.ErrorDescription!);
            }

            var request = new OpenIddictRequest
            {
                GrantType = grantType,
                ClientId = clientId,
                ClientSecret = fields["client_secret"].ToString(),
                Scope = fields["scope"].ToString()
            };
            request.SetParameter("corp_id", fields["corp_id"].ToString());
            request.SetParameter("object_type", fields["object_type"].ToString());
            request.SetParameter("object_id", fields["object_id"].ToString());

            return await HandleTokenRequestAsync(request, cancellationToken);
        }

        private static EncryptedFieldsResult ParseEncryptedFields(EncryptedLoginRequest body)
        {
            if (body == null || string.IsNullOrWhiteSpace(body.Encrypted))
            {
                return new EncryptedFieldsResult
                {
                    Result = new BadRequestObjectResult(new OpenIddictResponse
                    {
                        Error = Errors.InvalidRequest,
                        ErrorDescription = "The encrypted field is required."
                    })
                };
            }

            string json;
            try
            {
                var bytes = Convert.FromBase64String(body.Encrypted);
                json = Encoding.UTF8.GetString(bytes);
            }
            catch (FormatException)
            {
                return new EncryptedFieldsResult
                {
                    Result = new BadRequestObjectResult(new OpenIddictResponse
                    {
                        Error = Errors.InvalidRequest,
                        ErrorDescription = "The encrypted value is not a valid Base64 string."
                    })
                };
            }

            try
            {
                return new EncryptedFieldsResult
                {
                    Fields = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>()
                };
            }
            catch (JsonException)
            {
                return new EncryptedFieldsResult
                {
                    Result = new BadRequestObjectResult(new OpenIddictResponse
                    {
                        Error = Errors.InvalidRequest,
                        ErrorDescription = "The encrypted payload is not a valid JSON object."
                    })
                };
            }
        }

        private async Task<IActionResult> HandleTokenRequestAsync(OpenIddictRequest request, CancellationToken cancellationToken)
        {
            var result = await _tokenRequestHandler.HandleAsync(request, cancellationToken);
            if (!result.Succeeded)
            {
                return CreateErrorResult(result.Error!, result.ErrorDescription!);
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

        private IActionResult CreateErrorResult(string error, string description)
        {
            return Forbid(
                new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description
                }),
                [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }
    }

    public sealed class EncryptedLoginRequest
    {
        public string Encrypted { get; set; } = string.Empty;
    }

    internal sealed class EncryptedFieldsResult
    {
        public Dictionary<string, string>? Fields { get; set; }

        public IActionResult? Result { get; set; }
    }
}
