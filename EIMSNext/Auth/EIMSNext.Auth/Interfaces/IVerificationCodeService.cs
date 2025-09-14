using EIMSNext.Auth.Entity;

namespace EIMSNext.Auth.Interfaces
{
    public interface IVerificationCodeService
    {
        User? Validate(string? username, string? verifycode);
    }
}
