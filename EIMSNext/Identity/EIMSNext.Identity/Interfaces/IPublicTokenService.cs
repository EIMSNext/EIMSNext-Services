using EIMSNext.ApiService;
using EIMSNext.Identity.Models;

namespace EIMSNext.Identity.Interfaces
{
    public interface IPublicTokenService
    {
        PublicTokenValidationResult Validate(string? username, string? password, PublicScope scope);
    }
}
