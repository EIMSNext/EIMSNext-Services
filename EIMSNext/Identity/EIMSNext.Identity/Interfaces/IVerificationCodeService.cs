using EIMSNext.Entities;

namespace EIMSNext.Identity.Interfaces
{
    public interface IVerificationCodeService
    {
        User? Validate(string? username, string? verifycode);
    }
}
