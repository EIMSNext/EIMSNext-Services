using EIMSNext.Identity.AccountSecurity;
using EIMSNext.Entities;
using EIMSNext.Identity.Interfaces;
using EIMSNext.Identity.Utilities;
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
        /// <summary>门户登录授权码有效期（秒）</summary>
        private const int LoginCodeTtlSeconds = 60;

        private readonly IAccountSecurityService _accountSecurityService;
        private readonly ILogoutTokenStore _logoutTokenStore;
        private readonly ILogger<IdentityController> _logger;
        private readonly VerificationCodeRateLimiter _verificationCodeRateLimiter;
        private readonly IUserService _userService;
        private readonly IAuthorizationCodeStore _authorizationCodeStore;
        private readonly IBuiltInClientRequestPolicy _builtInClientRequestPolicy;
        private readonly PublicRateLimiter? _rateLimiter;

        public IdentityController(
            IAccountSecurityService accountSecurityService,
            ILogoutTokenStore logoutTokenStore,
            ILogger<IdentityController> logger,
            VerificationCodeRateLimiter verificationCodeRateLimiter,
            IUserService userService,
            IAuthorizationCodeStore authorizationCodeStore,
            IBuiltInClientRequestPolicy builtInClientRequestPolicy,
            PublicRateLimiter? rateLimiter = null)
        {
            _accountSecurityService = accountSecurityService;
            _logoutTokenStore = logoutTokenStore;
            _logger = logger;
            _verificationCodeRateLimiter = verificationCodeRateLimiter;
            _userService = userService;
            _authorizationCodeStore = authorizationCodeStore;
            _builtInClientRequestPolicy = builtInClientRequestPolicy;
            _rateLimiter = rateLimiter;
        }

        /// <summary>
        /// 门户密码登录签发一次性授权码（code 换 token 的凭证，60 秒有效、单次使用）。
        /// </summary>
        [Route("identity/authorizeCode"), HttpPost]
        [Consumes("application/x-www-form-urlencoded")]
        public async Task<IActionResult> AuthorizeCode([FromForm] EncryptedLoginRequest body)
        {
            var fieldsResult = TokenRequestHelper.ParseEncryptedFields(TokenRequestHelper.UnscrambleEncrypted(body?.Encrypted));
            if (!fieldsResult.Succeeded)
            {
                return BadRequest(new { message = fieldsResult.ErrorDescription });
            }

            var fields = fieldsResult.Fields!;
            if (!fields.TryGetValue("username", out var username) || string.IsNullOrWhiteSpace(username) ||
                !fields.TryGetValue("password", out var password) || string.IsNullOrWhiteSpace(password))
            {
                return BadRequest(new { message = "用户名和密码不能为空。" });
            }

            var clientId = fields.TryGetValue("client_id", out var clientIdValue) ? clientIdValue : null;
            var validation = _builtInClientRequestPolicy.ValidateLogin(clientId, Request);
            if (!validation.Succeeded)
            {
                return BadRequest(new { message = validation.ErrorDescription });
            }

            var user = _userService.Validate(username, password);
            if (user == null)
            {
                return await RejectLoginFailureAsync(username);
            }

            var code = await _authorizationCodeStore.IssueAsync(user.Id, username, clientId, TimeSpan.FromSeconds(LoginCodeTtlSeconds));
            return Ok(new { code, expires_in = LoginCodeTtlSeconds });
        }

        /// <summary>
        /// 已登录会话换取授权码：三方登录回调页持有 access_token 时，用它换 code 回传调用方，
        /// 避免 access_token 出现在跨源回跳 URL 上。
        /// </summary>
        [Authorize]
        [Route("identity/authorizeCodeSession"), HttpPost]
        public async Task<IActionResult> AuthorizeCodeSession()
        {
            var userId = GetCurrentUserId();
            var user = _userService.FindById(userId);
            if (user == null)
            {
                return Unauthorized();
            }

            var code = await _authorizationCodeStore.IssueAsync(user.Id, user.Name, InternalClients.WebClientId, TimeSpan.FromSeconds(LoginCodeTtlSeconds));
            return Ok(new { code, expires_in = LoginCodeTtlSeconds });
        }

        /// <summary>密码登录失败：按登录限流口径计数，超限返回 429。</summary>
        private async Task<IActionResult> RejectLoginFailureAsync(string username)
        {
            if (_rateLimiter != null)
            {
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var rate = await _rateLimiter.CheckAsync("login", username, ip);
                if (!rate.Allowed)
                {
                    return StatusCode(StatusCodes.Status429TooManyRequests, new { message = "登录失败次数过多，请稍后再试。" });
                }
            }

            return BadRequest(new { message = "用户不存在或密码错误" });
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

        /// <summary>
        /// 忘记密码：向已注册的手机/邮箱发送重置验证码（目标不存在时静默返回，防账号探测）。
        /// </summary>
        [Route("identity/sendResetCode"), HttpPost]
        public async Task<IActionResult> SendResetCode([FromBody] SendRegCodeRequest request)
        {
            try
            {
                if (request != null && (await IsCodeSendAllowedAsync("resetPassword", request.Target)).Allowed)
                {
                    await _accountSecurityService.SendResetCodeAsync(request);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Reset password verification code request rejected");
            }

            return Ok();
        }

        /// <summary>
        /// 忘记密码：凭重置验证码免登录重置密码。
        /// </summary>
        [Route("identity/resetPassword"), HttpPost]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            try
            {
                await _accountSecurityService.ResetPasswordAsync(request);
                return Ok(new { success = true });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
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

