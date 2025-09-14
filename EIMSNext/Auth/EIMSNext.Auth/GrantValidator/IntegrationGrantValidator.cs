using IdentityServer4.Validation;

using EIMSNext.Auth.Entity;
using EIMSNext.Auth.Interfaces;

namespace EIMSNext.Auth.GrantValidator
{
    public class IntegrationGrantValidator : IExtensionGrantValidator
    {
        private readonly IUserService _userService;
        public IntegrationGrantValidator(IUserService userService)
        {
            _userService = userService;
        }

        public string GrantType => CustomGrantType.Integration;

        public Task ValidateAsync(ExtensionGrantValidationContext context)
        {
            throw new NotImplementedException();
        }
    }
}
