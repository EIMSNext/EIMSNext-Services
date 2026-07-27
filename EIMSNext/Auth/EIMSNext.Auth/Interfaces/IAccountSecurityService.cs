using EIMSNext.Auth.AccountSecurity;

namespace EIMSNext.Auth.Interfaces
{
    public interface IAccountSecurityService
    {
        Task<VerificationCodeSendResult> SendRegCodeAsync(SendRegCodeRequest request);
        Task<VerificationCodeSendResult> SendLoginCodeAsync(SendRegCodeRequest request);
        Task RegisterAsync(RegisterRequest request);
        Task<VerificationCodeSendResult> SendPinCodeAsync(string userId, SendPinCodeRequest request);
        Task<VerifyIdentityResponse> VerifyIdentityAsync(string userId, VerifyIdentityRequest request);
        Task ChangePasswordAsync(string userId, ChangePasswordRequest request);
        Task ChangePhoneAsync(string userId, ChangePhoneRequest request);
        Task ChangeEmailAsync(string userId, ChangeEmailRequest request);
        Task UnbindPhoneAsync(string userId, UnbindPhoneRequest request);
        Task UnbindEmailAsync(string userId, UnbindEmailRequest request);
    }
}
