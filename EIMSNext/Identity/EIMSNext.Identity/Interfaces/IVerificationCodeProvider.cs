using EIMSNext.Identity.AccountSecurity;

namespace EIMSNext.Identity.Interfaces;

public interface IVerificationCodeProvider
{
    VerificationCodeSendResult Send(string purpose, string target);
    bool TryConsume(string purpose, string target, string? code);
}
