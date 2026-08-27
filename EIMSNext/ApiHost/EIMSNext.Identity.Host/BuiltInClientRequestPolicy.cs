using EIMSNext.Entities;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using OpenIddict.Abstractions;

namespace EIMSNext.Identity.Host
{
    public interface IBuiltInClientRequestPolicy
    {
        BuiltInClientValidationResult ValidateTokenEndpoint(string? clientId);

        BuiltInClientValidationResult ValidateLogin(string? clientId, HttpRequest request);

        BuiltInClientValidationResult ValidatePublicToken(string? clientId, string? grantType, HttpRequest request);

        BuiltInClientValidationResult ValidateSystemToken(string? clientId, string? grantType);
    }

    public sealed class BuiltInClientRequestPolicy : IBuiltInClientRequestPolicy
    {
        private readonly BuiltInClientsOptions _options;
        private readonly IHostEnvironment _environment;

        public BuiltInClientRequestPolicy(IOptions<BuiltInClientsOptions> options, IHostEnvironment environment)
        {
            _options = options.Value;
            _environment = environment;
        }

        public BuiltInClientValidationResult ValidateTokenEndpoint(string? clientId)
        {
            if (IsClient(clientId, InternalClients.WebClientId) ||
                IsClient(clientId, InternalClients.PublicClientId) ||
                IsClient(clientId, InternalClients.SystemClientId))
            {
                return BuiltInClientValidationResult.Failure(
                    OpenIddictConstants.Errors.InvalidClient,
                    "The client application is not allowed to use this endpoint.");
            }

            return BuiltInClientValidationResult.Success();
        }

        public BuiltInClientValidationResult ValidateLogin(string? clientId, HttpRequest request)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return BuiltInClientValidationResult.Failure(
                    OpenIddictConstants.Errors.InvalidClient,
                    "The client_id field is required.");
            }

            if (!IsClient(clientId, InternalClients.WebClientId))
            {
                return BuiltInClientValidationResult.Failure(
                    OpenIddictConstants.Errors.InvalidClient,
                    "The client application is not allowed to use this endpoint.");
            }

            return ValidateOrigin(_options.Web, request);
        }

        public BuiltInClientValidationResult ValidatePublicToken(string? clientId, string? grantType, HttpRequest request)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return BuiltInClientValidationResult.Failure(
                    OpenIddictConstants.Errors.InvalidClient,
                    "The client_id field is required.");
            }

            if (!IsClient(clientId, InternalClients.PublicClientId))
            {
                return BuiltInClientValidationResult.Failure(
                    OpenIddictConstants.Errors.UnauthorizedClient,
                    "The client application is not allowed to use this endpoint.");
            }

            if (string.IsNullOrWhiteSpace(grantType) ||
                !string.Equals(grantType, EIMSNext.Entities.CustomGrantType.Public, StringComparison.Ordinal))
            {
                return BuiltInClientValidationResult.Failure(
                    OpenIddictConstants.Errors.InvalidRequest,
                    "The grant_type field must be 'public'.");
            }

            return ValidateOrigin(_options.Public, request);
        }

        public BuiltInClientValidationResult ValidateSystemToken(string? clientId, string? grantType)
        {
            if (!IsClient(clientId, InternalClients.SystemClientId))
            {
                return BuiltInClientValidationResult.Failure(
                    OpenIddictConstants.Errors.UnauthorizedClient,
                    "The client application is not allowed to use this endpoint.");
            }

            if (!string.Equals(grantType, CustomGrantType.System, StringComparison.Ordinal))
            {
                return BuiltInClientValidationResult.Failure(
                    OpenIddictConstants.Errors.InvalidRequest,
                    "The grant_type field must be 'system'.");
            }

            return BuiltInClientValidationResult.Success();
        }

        private BuiltInClientValidationResult ValidateOrigin(BuiltInClientPolicyOptions policy, HttpRequest request)
        {
            if (!policy.RequireOrigin)
            {
                return BuiltInClientValidationResult.Success();
            }

            var origin = request.Headers.Origin.ToString();
            if (string.IsNullOrWhiteSpace(origin))
            {
                if (_environment.IsDevelopment() && policy.AllowMissingOriginInDevelopment)
                {
                    return BuiltInClientValidationResult.Success();
                }

                return BuiltInClientValidationResult.Failure(
                    OpenIddictConstants.Errors.InvalidRequest,
                    "The Origin header is required.");
            }

            var normalizedOrigin = NormalizeOrigin(origin);
            if (normalizedOrigin == null)
            {
                return BuiltInClientValidationResult.Failure(
                    OpenIddictConstants.Errors.InvalidRequest,
                    "The Origin header is invalid.");
            }

            var allowed = policy.AllowedOrigins
                .Select(NormalizeOrigin)
                .Where(x => x != null)
                .Cast<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!allowed.Contains(normalizedOrigin))
            {
                return BuiltInClientValidationResult.Failure(
                    OpenIddictConstants.Errors.InvalidClient,
                    "The client origin is not allowed.");
            }

            return BuiltInClientValidationResult.Success();
        }

        private static bool IsClient(string? actualClientId, string? expectedClientId)
        {
            return !string.IsNullOrWhiteSpace(actualClientId)
                && !string.IsNullOrWhiteSpace(expectedClientId)
                && string.Equals(actualClientId, expectedClientId, StringComparison.Ordinal);
        }

        private static string? NormalizeOrigin(string? origin)
        {
            if (string.IsNullOrWhiteSpace(origin) || !Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            {
                return null;
            }

            return uri.GetLeftPart(UriPartial.Authority);
        }
    }

    public sealed record BuiltInClientValidationResult(bool Succeeded, string? Error, string? ErrorDescription)
    {
        public static BuiltInClientValidationResult Success() => new(true, null, null);

        public static BuiltInClientValidationResult Failure(string error, string description) => new(false, error, description);
    }
}
