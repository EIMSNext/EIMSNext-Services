using EIMSNext.Auth.AccountSecurity;

namespace EIMSNext.Auth.Interfaces;

public interface IVerificationCodeProvider
{
    VerificationCodeSendResult Send(string purpose, string target);
    bool TryConsume(string purpose, string target, string? code);
}
