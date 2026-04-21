using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Models;
using OpenIddict.Abstractions;

namespace EIMSNext.Auth.Interfaces
{
    public interface ITokenGrantHandler
    {
        string GrantType { get; }

        Task<TokenRequestResult> HandleAsync(Client client, OpenIddictRequest request, IReadOnlyList<string> scopes, CancellationToken cancellationToken = default);
    }
}
