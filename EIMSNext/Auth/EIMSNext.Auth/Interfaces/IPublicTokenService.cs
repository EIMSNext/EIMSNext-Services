using EIMSNext.ApiService;
using EIMSNext.Auth.Models;

namespace EIMSNext.Auth.Interfaces
{
    public interface IPublicTokenService
    {
        PublicTokenValidationResult Validate(string? username, string? password, PublicScope scope);
    }
}
