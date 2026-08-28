using OpenIddict.Abstractions;
using EIMSNext.Identity.Models;

namespace EIMSNext.Identity.Interfaces
{
    public interface ITokenRequestHandler
    {
        Task<TokenRequestResult> HandleAsync(OpenIddictRequest request, CancellationToken cancellationToken = default);
    }
}
