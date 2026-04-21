using EIMSNext.Auth.Entities;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.Auth.Host.Controllers
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

            AddClaim(payload, AuthClaimTypes.Subject, User.FindFirstValue(AuthClaimTypes.Subject) ?? User.FindFirstValue(ClaimTypes.NameIdentifier));
            AddClaim(payload, AuthClaimTypes.Name, User.FindFirstValue(AuthClaimTypes.Name));
            AddClaim(payload, AuthClaimTypes.Id, User.FindFirstValue(AuthClaimTypes.Id));
            AddClaim(payload, AuthClaimTypes.Corp, User.FindFirstValue(AuthClaimTypes.Corp));
            AddClaim(payload, AuthClaimTypes.ClientId, User.FindFirstValue(AuthClaimTypes.ClientId));
            AddLongClaim(payload, AuthClaimTypes.AuthTime, User.FindFirstValue(AuthClaimTypes.AuthTime));

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
