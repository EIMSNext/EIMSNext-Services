using OpenIddict.Abstractions;
using EIMSNext.Auth.Models;

namespace EIMSNext.Auth.Interfaces
{
    public interface ITokenRequestHandler
    {
        Task<TokenRequestResult> HandleAsync(OpenIddictRequest request, CancellationToken cancellationToken = default);
    }
}
