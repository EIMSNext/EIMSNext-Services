using System.Security.Claims;
using IdentityModel;
using IdentityServer4.Models;
using IdentityServer4.Validation;
using EIMSNext.Auth.Interfaces;

namespace EIMSNext.Auth.GrantValidator
{
    public class ResourceUserPasswordValidator : IResourceOwnerPasswordValidator
    {
        private readonly IUserService _userService;
        public ResourceUserPasswordValidator(IUserService userService)
        {
            _userService = userService;
        }
        public Task ValidateAsync(ResourceOwnerPasswordValidationContext context)
        {
            var userName = context.UserName;
            var password = context.Password;
            var user = _userService.Validate(userName, password);

            if (user == null)
            {
                context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "用户不存在或密码错误");
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

                context.Result = new GrantValidationResult(userName, OidcConstants.AuthenticationMethods.Password, DateTime.UtcNow, claims);
            }

            return Task.CompletedTask;
        }
    }
}
