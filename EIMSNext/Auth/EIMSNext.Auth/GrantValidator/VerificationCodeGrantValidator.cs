using System.Security.Claims;
using EIMSNext.Auth.Entity;
using EIMSNext.Auth.Interfaces;
using IdentityModel;
using IdentityServer4.Models;
using IdentityServer4.Validation;

namespace EIMSNext.Auth.GrantValidator
{
    public class VerificationCodeGrantValidator : IExtensionGrantValidator
    {
        private readonly IVerificationCodeService _verificationCodeService;

        public VerificationCodeGrantValidator(IVerificationCodeService verificationCodeService)
        {
            _verificationCodeService = verificationCodeService;
        }
        public string GrantType => CustomGrantType.VerificationCode;

        public Task ValidateAsync(ExtensionGrantValidationContext context)
        {
            var username = context.Request.Raw["username"];
            var verifycode = context.Request.Raw["password"];
            var user = _verificationCodeService.Validate(username, verifycode);
            if (user == null)
            {
                context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "用户不存在或验证码错误");
            }
            else
            {
                var corp = user.Crops.FirstOrDefault(x => x.IsDefault);
                if (corp == null || string.IsNullOrEmpty(corp.CorpId))
                {
                    corp = user.Crops.FirstOrDefault(x => x.IsCorpOwner);
                }
                var claims = new List<Claim>() {
                        new Claim(JwtClaimTypes.Name, user.Name),
                        new Claim(JwtClaimTypes.Id, user.Id),
                        new Claim("corp", corp?.CorpId??string.Empty)
                    };

                context.Result = new GrantValidationResult(username, GrantType, DateTime.UtcNow, claims);
            }

            return Task.CompletedTask;
        }
    }
}
