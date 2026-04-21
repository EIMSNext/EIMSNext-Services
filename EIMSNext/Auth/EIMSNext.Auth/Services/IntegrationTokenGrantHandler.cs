using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Interfaces;
using EIMSNext.Auth.Models;
using OpenIddict.Abstractions;

namespace EIMSNext.Auth.Services
{
    public sealed class IntegrationTokenGrantHandler : ITokenGrantHandler
    {
        public string GrantType => CustomGrantType.Integration;

        public Task<TokenRequestResult> HandleAsync(Client client, OpenIddictRequest request, IReadOnlyList<string> scopes, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(TokenRequestResult.Failure(OpenIddictConstants.Errors.UnsupportedGrantType, "The specified grant type is not supported."));
        }
    }
}
