using System.Reflection;
using EIMSNext.Auth.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.Auth.Host.Controllers
{
    /// <summary>
    /// 所有方法都允许匿名访问，无需登录
    /// </summary>
    [ApiController]
    public class OpenController : ControllerBase
    {
        private readonly IIntegrationAuthService _integrationAuthService;

        public OpenController(IIntegrationAuthService integrationAuthService)
        {
            _integrationAuthService = integrationAuthService;
        }

        [Route("api/ping"), HttpGet]
        public string Ping()
        {
            return "Auth Server is running.";
        }

        [Route("api/version"), HttpGet]
        public string Version()
        {
            return Assembly.GetExecutingAssembly().GetName().Version!.ToString();
        }

        [Route("api/open/integration/authorize"), HttpGet]
        public async Task<IActionResult> GetIntegrationAuthorizationUrl([FromQuery] string type, [FromQuery] string state, CancellationToken cancellationToken)
        {
            var result = await _integrationAuthService.GetAuthorizationUrlAsync(type, state, cancellationToken);
            if (!result.Enabled || string.IsNullOrWhiteSpace(result.AuthorizationUrl))
            {
                return BadRequest(new { message = $"{type} 集成登录未启用或配置不完整" });
            }

            return Ok(result);
        }
    }
}
