using System.Security.Claims;
using EIMSNext.ApiCore;
using EIMSNext.Auth.Entity;
using EIMSNext.Auth.Interfaces;
using EIMSNext.Common.Extension;
using IdentityModel;
using IdentityServer4.Models;
using IdentityServer4.Validation;
using Microsoft.AspNetCore.Http;

namespace EIMSNext.Auth.GrantValidator
{
    public class VerificationCodeGrantValidator : IExtensionGrantValidator
    {
        private readonly IVerificationCodeService _verificationCodeService;
        private readonly IAuditLoginService _auditLoginService;
        private readonly IHttpContextAccessor _contextAccessor;

        public VerificationCodeGrantValidator(IVerificationCodeService verificationCodeService, IAuditLoginService auditLoginService,
            IHttpContextAccessor httpContextAccessor)
        {
            _verificationCodeService = verificationCodeService;
            _auditLoginService = auditLoginService;
            _contextAccessor = httpContextAccessor;
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
                _auditLoginService.AddAuditLogin(
                    new AuditLogin
                    {
                        LoginId = username,
                        ClientId = Constants.ClientId_Web,
                        ClientIp = IpHelper.GetClientIp(_contextAccessor),
                        CreateTime = DateTime.UtcNow.ToTimeStampMs(),
                        GrantType = "password",
                        FailReason = "用户不存在或密码错误"
                    });
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
                _auditLoginService.AddAuditLogin(
                    new AuditLogin
                    {
                        LoginId = username,
                        UserId = user.Id,
                        UserName = user.Name,
                        CorpId = corp?.CorpId,
                        ClientId = Constants.ClientId_Web,
                        ClientIp = IpHelper.GetClientIp(_contextAccessor),
                        CreateTime = DateTime.UtcNow.ToTimeStampMs(),
                        GrantType = "verifycode"
                    });
            }

            return Task.CompletedTask;
        }
    }
}
