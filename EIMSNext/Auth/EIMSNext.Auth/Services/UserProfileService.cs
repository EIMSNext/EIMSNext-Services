using IdentityServer4.Models;
using IdentityServer4.Services;

namespace EIMSNext.Auth.Services
{
    public class UserProfileService : IProfileService
    {
        public Task GetProfileDataAsync(ProfileDataRequestContext context)
        {
            try
            {
                //depending on the scope accessing the user data.
                var claims = context.Subject.Claims.ToList();
                //set issued claims to return
                context.IssuedClaims = claims.ToList();
            }
            catch
            {
                //log your error
            }
            return Task.CompletedTask;
        }

        public Task IsActiveAsync(IsActiveContext context)
        {
            context.IsActive = true;
            return Task.CompletedTask;
        }
    }
}
