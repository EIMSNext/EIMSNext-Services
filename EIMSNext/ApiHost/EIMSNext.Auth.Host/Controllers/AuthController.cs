using EIMSNext.Auth.AccountSecurity;
using EIMSNext.Auth.Interfaces;
using IdentityModel;
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

        public AuthController(IUserService userService, IAccountSecurityService accountSecurityService)
        {
            _userService = userService;
            _accountSecurityService = accountSecurityService;
        }

        /// <summary>
        /// 集成登录方式获取Token
        /// </summary>
        /// <returns></returns>
        [Route("auth/sendcode"), HttpPost]
        public IActionResult SendCode()
        {
            return NoContent();
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
            var userId = User.FindFirst(JwtClaimTypes.Id)?.Value ?? User.FindFirst("sub")?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new UnauthorizedAccessException("未登录");
            }

            return userId;
        }
    }
}
