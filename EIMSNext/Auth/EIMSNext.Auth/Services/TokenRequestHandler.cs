using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Interfaces;
using EIMSNext.Auth.Models;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace EIMSNext.Auth.Services
{
    public class TokenRequestHandler : ITokenRequestHandler
    {
        private readonly IAuthDbContext _context;
        private readonly IReadOnlyDictionary<string, ITokenGrantHandler> _grantHandlers;

        public TokenRequestHandler(
            IAuthDbContext context,
            IEnumerable<ITokenGrantHandler> grantHandlers)
        {
            _context = context;
            _grantHandlers = grantHandlers.ToDictionary(x => x.GrantType, StringComparer.Ordinal);
        }

        public async Task<TokenRequestResult> HandleAsync(OpenIddictRequest request, CancellationToken cancellationToken = default)
        {
            var client = ValidateClient(request.ClientId, request.ClientSecret);
            if (client == null)
            {
                return TokenRequestResult.Failure(Errors.InvalidClient, "The specified client credentials are invalid.");
            }

            var scopes = request.GetScopes().ToArray();
            if (scopes.Length == 0)
            {
                scopes = client.AllowedScopes
                    .Select(x => x.Scope)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Cast<string>()
                    .ToArray();
            }

            if (scopes.Length == 0 || !AreScopesAllowed(client, scopes))
            {
                return TokenRequestResult.Failure(Errors.InvalidScope, "The specified scope is invalid.");
            }

            if (!IsGrantTypeAllowed(client, request.GrantType))
            {
                return TokenRequestResult.Failure(Errors.UnauthorizedClient, "The client application is not allowed to use this grant type.");
            }

            if (string.IsNullOrWhiteSpace(request.GrantType) || !_grantHandlers.TryGetValue(request.GrantType, out var grantHandler))
            {
                return TokenRequestResult.Failure(Errors.UnsupportedGrantType, "The specified grant type is not supported.");
            }

            return await grantHandler.HandleAsync(client, request, scopes, cancellationToken);
        }

        private Client? ValidateClient(string? clientId, string? clientSecret)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return null;
            }

            var client = _context.Clients.FirstOrDefault(x => x.Id == clientId && x.Enabled);
            if (client == null)
            {
                return null;
            }

            if (!client.RequireClientSecret)
            {
                return client;
            }

            if (string.IsNullOrWhiteSpace(clientSecret))
            {
                return null;
            }

            var hashed = clientSecret.Sha256();
            return client.ClientSecrets.Any(x => string.Equals(x.Value, hashed, StringComparison.Ordinal)) ? client : null;
        }

        private static bool IsGrantTypeAllowed(Client client, string? grantType)
        {
            return !string.IsNullOrWhiteSpace(grantType) && client.AllowedGrantTypes.Any(x => string.Equals(x.GrantType, grantType, StringComparison.Ordinal));
        }

        private static bool AreScopesAllowed(Client client, IReadOnlyCollection<string> scopes)
        {
            if (scopes.Count == 0)
            {
                return false;
            }

            var allowed = client.AllowedScopes.Select(x => x.Scope).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.Ordinal);
            return scopes.All(scope => allowed.Contains(scope));
        }
    }
}
