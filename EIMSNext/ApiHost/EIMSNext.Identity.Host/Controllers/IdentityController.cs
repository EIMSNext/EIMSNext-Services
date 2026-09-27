using EIMSNext.Identity.AccountSecurity;
using EIMSNext.Entities;
using EIMSNext.Identity.Interfaces;
using EIMSNext.ApiCore;
using EIMSNext.ApiCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.Identity.Host.Controllers
{
    /// <summary>
    /// 所有方法都允许匿名访问，无需登录
    /// </summary>
    [ApiController]
    public class IdentityController : ControllerBase
    {
        private readonly IAccountSecurityService _accountSecurityService;
        private readonly ILogoutTokenStore _logoutTokenStore;
        private readonly ILogger<IdentityController> _logger;
        private readonly VerificationCodeRateLimiter _verificationCodeRateLimiter;

        public IdentityController(
            IAccountSecurityService accountSecurityService,
            ILogoutTokenStore logoutTokenStore,
            ILogger<IdentityController> logger,
            VerificationCodeRateLimiter verificationCodeRateLimiter)
        {
            _accountSecurityService = accountSecurityService;
            _logoutTokenStore = logoutTokenStore;
            _logger = logger;
            _verificationCodeRateLimiter = verificationCodeRateLimiter;
        }

        /// <summary>
        /// 集成登录方式获取Token
        /// </summary>
        /// <returns></returns>
        [Route("identity/sendRegCode"), HttpPost]
        public async Task<IActionResult> SendRegCode([FromBody] SendRegCodeRequest request)
        {
            try
            {
                if (request != null && (await IsCodeSendAllowedAsync("register", request.Target)).Allowed)
                {
                    await _accountSecurityService.SendRegCodeAsync(request);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Registration verification code request rejected");
            }

            return Ok();
        }

        /// <summary>
        /// 集成登录方式获取Token
        /// </summary>
        /// <returns></returns>
        [Route("identity/register"), HttpPost]
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
        [Route("identity/logout"), HttpPost]
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
        [Route("identity/sendPinCode"), HttpPost]
        public async Task<IActionResult> SendPinCode([FromBody] SendPinCodeRequest request)
        {
            try
            {
                if (request != null && (await IsCodeSendAllowedAsync(request.Usage, request.Target)).Allowed)
                {
                    await _accountSecurityService.SendPinCodeAsync(GetCurrentUserId(), request);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PIN verification code request rejected");
            }

            return Ok();
        }

        [Authorize]
        [Route("identity/verifyIdentity"), HttpPost]
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
        [Route("identity/changePassword"), HttpPost]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
        {
            try
            {
                await _accountSecurityService.ChangePasswordAsync(GetCurrentUserId(), request);
                await InvalidateCurrentTokenAsync(cancellationToken);
                return Ok(new { success = true });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Route("identity/sendLoginCode"), HttpPost]
        public async Task<IActionResult> SendLoginCode([FromBody] SendRegCodeRequest request)
        {
            try
            {
                if (request != null && (await IsCodeSendAllowedAsync("login", request.Target)).Allowed)
                {
                    await _accountSecurityService.SendLoginCodeAsync(request);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Login verification code request rejected");
            }

            return Ok();
        }

        private Task<VerificationCodeRateLimitResult> IsCodeSendAllowedAsync(string? purpose, string? target)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return _verificationCodeRateLimiter.CheckAsync(purpose ?? string.Empty, target ?? string.Empty, ip);
        }

        private async Task InvalidateCurrentTokenAsync(CancellationToken cancellationToken)
        {
            var token = LogoutTokenHelper.ReadBearerToken(Request);
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            var expiresAt = LogoutTokenHelper.ReadExpirationUtc(token);
            if (expiresAt is null || expiresAt <= DateTimeOffset.UtcNow)
            {
                return;
            }

            await _logoutTokenStore.MarkLoggedOutAsync(token, expiresAt.Value, cancellationToken);
        }

        [Authorize]
        [Route("identity/changePhone"), HttpPost]
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
        [Route("identity/changeEmail"), HttpPost]
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
        [Route("identity/unbindPhone"), HttpPost]
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
        [Route("identity/unbindEmail"), HttpPost]
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
            var userId = User.FindFirst(IdentityClaimTypes.Id)?.Value ?? User.FindFirst(IdentityClaimTypes.Subject)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new UnauthorizedAccessException("未登录");
            }

            return userId;
        }

    }

}
