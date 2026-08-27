using EIMSNext.Entities;
using EIMSNext.Identity.Models;
using OpenIddict.Abstractions;

namespace EIMSNext.Identity.Interfaces
{
    public interface ITokenGrantHandler
    {
        string GrantType { get; }

        Task<TokenRequestResult> HandleAsync(Client client, OpenIddictRequest request, IReadOnlyList<string> scopes, CancellationToken cancellationToken = default);
    }
}
