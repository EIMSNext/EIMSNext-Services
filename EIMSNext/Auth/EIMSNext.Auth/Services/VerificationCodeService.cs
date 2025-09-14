using EIMSNext.Auth.Entity;
using EIMSNext.Auth.Interfaces;

namespace EIMSNext.Auth.Services
{
    public class VerificationCodeService : IVerificationCodeService
    {
        private readonly IUserService _userService;
        public VerificationCodeService(IUserService userService)
        {
            _userService = userService;
        }
        public User? Validate(string? username, string? verifycode)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(verifycode) || !VerifyCode(username, verifycode))
            {
                return null;
            }

            return _userService.FindByEmailOrPhone(username);
        }
        private bool VerifyCode(string username, string? verifycode)
        {
            return true;
        }
    }
}
