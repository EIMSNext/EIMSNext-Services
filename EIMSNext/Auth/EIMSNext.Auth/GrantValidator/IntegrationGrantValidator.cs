using IdentityServer4.Validation;

using EIMSNext.Auth.Entity;
using EIMSNext.Auth.Interfaces;
using Microsoft.AspNetCore.Http;

namespace EIMSNext.Auth.GrantValidator
{
    public class IntegrationGrantValidator : IExtensionGrantValidator
    {
        private readonly IUserService _userService;
        private readonly IAuditLoginService _auditLoginService;
        private readonly IHttpContextAccessor _contextAccessor;
        public IntegrationGrantValidator(IUserService userService, IAuditLoginService auditLoginService,
            IHttpContextAccessor httpContextAccessor)
        {
            _userService = userService;
            _auditLoginService = auditLoginService;
            _contextAccessor = httpContextAccessor;
        }

        public string GrantType => CustomGrantType.Integration;

        public Task ValidateAsync(ExtensionGrantValidationContext context)
        {
            throw new NotImplementedException();
        }
    }
}
