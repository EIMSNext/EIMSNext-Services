using EIMSNext.Auth.Entities;

namespace EIMSNext.Auth.Interfaces
{
    public interface IVerificationCodeService
    {
        User? Validate(string? username, string? verifycode);
    }
}
