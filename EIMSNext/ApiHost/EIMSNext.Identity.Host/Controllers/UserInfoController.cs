using EIMSNext.Entities;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.Identity.Host.Controllers
{
    [ApiController]
    public class UserInfoController : ControllerBase
    {
        [Authorize]
        [HttpGet("~/connect/userinfo")]
        [HttpPost("~/connect/userinfo")]
        [Produces("application/json")]
        public IActionResult GetUserInfo()
        {
            var payload = new Dictionary<string, object>(StringComparer.Ordinal);

            AddClaim(payload, IdentityClaimTypes.Subject, User.FindFirstValue(IdentityClaimTypes.Subject) ?? User.FindFirstValue(ClaimTypes.NameIdentifier));
            AddClaim(payload, IdentityClaimTypes.Name, User.FindFirstValue(IdentityClaimTypes.Name));
            AddClaim(payload, IdentityClaimTypes.Id, User.FindFirstValue(IdentityClaimTypes.Id));
            AddClaim(payload, IdentityClaimTypes.Corp, User.FindFirstValue(IdentityClaimTypes.Corp));
            AddClaim(payload, IdentityClaimTypes.ClientId, User.FindFirstValue(IdentityClaimTypes.ClientId));
            AddLongClaim(payload, IdentityClaimTypes.AuthTime, User.FindFirstValue(IdentityClaimTypes.AuthTime));

            return Ok(payload);
        }

        private static void AddClaim(IDictionary<string, object> payload, string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                payload[key] = value;
            }
        }

        private static void AddLongClaim(IDictionary<string, object> payload, string key, string? value)
        {
            if (long.TryParse(value, out var parsedValue))
            {
                payload[key] = parsedValue;
            }
        }
    }
}
