using EIMSNext.Entities;
using EIMSNext.Identity.Interfaces;
using EIMSNext.Identity.AccountSecurity;

namespace EIMSNext.Identity.Services
{
    public class VerificationCodeService : IVerificationCodeService
    {
        private readonly IUserService _userService;
        private readonly IVerificationCodeProvider _verificationCodeProvider;

        public VerificationCodeService(
            IUserService userService,
            IVerificationCodeProvider? verificationCodeProvider = null)
        {
            _userService = userService;
            _verificationCodeProvider = verificationCodeProvider ?? new MockVerificationCodeProvider();
        }

        public User? Validate(string? username, string? verifycode)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(verifycode))
            {
                return null;
            }

            var user = _userService.FindByEmailOrPhone(username.Trim());
            return user != null &&
                   _verificationCodeProvider.TryConsume(VerificationCodePurpose.Login, username, verifycode)
                ? user
                : null;
        }
    }
}
