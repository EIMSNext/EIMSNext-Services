using System.Security.Claims;
using IdentityModel;
using IdentityServer4.Models;
using IdentityServer4.Validation;
using EIMSNext.Auth.Interfaces;
using EIMSNext.Auth.Entity;
using EIMSNext.ApiCore;
using Microsoft.AspNetCore.Http;
using EIMSNext.Common.Extension;

namespace EIMSNext.Auth.GrantValidator
{
    public class ResourceUserPasswordValidator : IResourceOwnerPasswordValidator
    {
        private readonly IUserService _userService;
        private readonly IAuditLoginService _auditLoginService;
        private readonly IHttpContextAccessor _contextAccessor;
        public ResourceUserPasswordValidator(IUserService userService, IAuditLoginService auditLoginService,
            IHttpContextAccessor httpContextAccessor)
        {
            _userService = userService;
            _auditLoginService = auditLoginService;
            _contextAccessor = httpContextAccessor;
        }
        public Task ValidateAsync(ResourceOwnerPasswordValidationContext context)
        {
            var username = context.UserName;
            var password = context.Password;
            var user = _userService.Validate(username, password);

            if (user == null)
            {
                context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "用户不存在或密码错误");
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

                //TODO: 登录失败需要记录次数，防止暴力破解
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

                context.Result = new GrantValidationResult(username, OidcConstants.AuthenticationMethods.Password, DateTime.UtcNow, claims);

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
                        GrantType = "password"
                    });
            }

            return Task.CompletedTask;
        }
    }
}
