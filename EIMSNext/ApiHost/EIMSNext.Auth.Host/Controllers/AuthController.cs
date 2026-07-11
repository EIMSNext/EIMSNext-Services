using EIMSNext.Auth.AccountSecurity;
using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Interfaces;
using EIMSNext.ApiCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.Auth.Host.Controllers
{
    /// <summary>
    /// 所有方法都允许匿名访问，无需登录
    /// </summary>
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IAccountSecurityService _accountSecurityService;
        private readonly ILogoutTokenStore _logoutTokenStore;

        public AuthController(IUserService userService, IAccountSecurityService accountSecurityService, ILogoutTokenStore logoutTokenStore)
        {
            _userService = userService;
            _accountSecurityService = accountSecurityService;
            _logoutTokenStore = logoutTokenStore;
        }

        /// <summary>
        /// 集成登录方式获取Token
        /// </summary>
        /// <returns></returns>
        [Route("auth/sendRegCode"), HttpPost]
        public async Task<IActionResult> SendRegCode([FromBody] SendRegCodeRequest request)
        {
            try
            {
                await _accountSecurityService.SendRegCodeAsync(request);
                return Ok(new { success = true });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 集成登录方式获取Token
        /// </summary>
        /// <returns></returns>
        [Route("auth/register"), HttpPost]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                await _accountSecurityService.RegisterAsync(request);
                return Ok(new { success = true });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize]
        [Route("auth/logout"), HttpPost]
        public async Task<IActionResult> Logout(CancellationToken cancellationToken)
        {
            var token = LogoutTokenHelper.ReadBearerToken(Request);
            if (string.IsNullOrWhiteSpace(token))
            {
                return Unauthorized();
            }

            var expiresAt = LogoutTokenHelper.ReadExpirationUtc(token);
            if (expiresAt is null || expiresAt <= DateTimeOffset.UtcNow)
            {
                return Ok(new { success = true });
            }

            await _logoutTokenStore.MarkLoggedOutAsync(token, expiresAt.Value, cancellationToken);
            return Ok(new { success = true });
        }

        [Authorize]
        [Route("auth/sendPinCode"), HttpPost]
        public async Task<IActionResult> SendPinCode([FromBody] SendPinCodeRequest request)
        {
            try
            {
                await _accountSecurityService.SendPinCodeAsync(GetCurrentUserId(), request);
                return Ok(new { success = true });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize]
        [Route("auth/verifyIdentity"), HttpPost]
        public async Task<IActionResult> VerifyIdentity([FromBody] VerifyIdentityRequest request)
        {
            try
            {
                var result = await _accountSecurityService.VerifyIdentityAsync(GetCurrentUserId(), request);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize]
        [Route("auth/changePassword"), HttpPost]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            try
            {
                await _accountSecurityService.ChangePasswordAsync(GetCurrentUserId(), request);
                return Ok(new { success = true });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize]
        [Route("auth/changePhone"), HttpPost]
        public async Task<IActionResult> ChangePhone([FromBody] ChangePhoneRequest request)
        {
            try
            {
                await _accountSecurityService.ChangePhoneAsync(GetCurrentUserId(), request);
                return Ok(new { success = true });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize]
        [Route("auth/changeEmail"), HttpPost]
        public async Task<IActionResult> ChangeEmail([FromBody] ChangeEmailRequest request)
        {
            try
            {
                await _accountSecurityService.ChangeEmailAsync(GetCurrentUserId(), request);
                return Ok(new { success = true });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize]
        [Route("auth/unbindPhone"), HttpPost]
        public async Task<IActionResult> UnbindPhone([FromBody] UnbindPhoneRequest request)
        {
            try
            {
                await _accountSecurityService.UnbindPhoneAsync(GetCurrentUserId(), request);
                return Ok(new { success = true });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize]
        [Route("auth/unbindEmail"), HttpPost]
        public async Task<IActionResult> UnbindEmail([FromBody] UnbindEmailRequest request)
        {
            try
            {
                await _accountSecurityService.UnbindEmailAsync(GetCurrentUserId(), request);
                return Ok(new { success = true });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private string GetCurrentUserId()
        {
            var userId = User.FindFirst(AuthClaimTypes.Id)?.Value ?? User.FindFirst(AuthClaimTypes.Subject)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new UnauthorizedAccessException("未登录");
            }

            return userId;
        }
    }
}
