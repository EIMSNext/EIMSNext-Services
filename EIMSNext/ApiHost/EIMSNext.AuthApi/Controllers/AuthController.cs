using EIMSNext.Auth.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.AuthApi.Controllers
{
    /// <summary>
    /// 所有方法都允许匿名访问，无需登录
    /// </summary>
    [ApiController]
    public class AuthController : Controller
    {
        private readonly IUserService _userService;
        public AuthController(IUserService userService)
        {
            _userService = userService;
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
    }
}
