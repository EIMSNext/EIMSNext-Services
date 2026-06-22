using EIMSNext.ApiService;
using EIMSNext.Auth.Models;

namespace EIMSNext.Auth.Interfaces
{
    public interface IPublicTokenService
    {
        PublicTokenSubject? Validate(string? username, string? password, PublicScope scope);
    }
}
