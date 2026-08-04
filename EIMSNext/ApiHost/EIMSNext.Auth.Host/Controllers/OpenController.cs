using EIMSNext.ApiCore.RateLimiting;
using EIMSNext.Auth.Models;
using EIMSNext.Auth.Services;
using EIMSNext.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RestSharp;
using System.Reflection;

namespace EIMSNext.Auth.Host.Controllers
{
    /// <summary>
    /// 所有方法都允许匿名访问，无需登录
    /// </summary>
    [ApiController]
    public class OpenController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<OpenController> _logger;
        private readonly PublicAccessOptions _publicAccessOptions;
        private readonly PublicSettingLookupService _publicSettingLookup;
        private readonly PublicRateLimiter _rateLimiter;

        public OpenController(
            IConfiguration configuration,
            ILogger<OpenController> logger,
            IOptions<PublicAccessOptions> publicAccessOptions,
            PublicSettingLookupService publicSettingLookup,
            PublicRateLimiter rateLimiter)
        {
            _configuration = configuration;
            _logger = logger;
            _publicAccessOptions = publicAccessOptions.Value;
            _publicSettingLookup = publicSettingLookup;
            _rateLimiter = rateLimiter;
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

        [Route("api/public/challenge"), HttpGet]
        public async Task<IActionResult> GetPublicChallenge([FromQuery] string? targetId)
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return BadRequest(new { message = "targetId 不能为空" });
            }

            if (string.IsNullOrWhiteSpace(_publicAccessOptions.SecretKey))
            {
                _logger.LogError("PublicAccess:SecretKey 未配置");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "PublicAccess:SecretKey 未配置" });
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var rate = await _rateLimiter.CheckAsync("challenge", targetId, ip);
            if (!rate.Allowed)
            {
                _logger.LogWarning("公开 challenge 限流命中 ip={Ip} targetId={TargetId} count={Count}", ip, targetId, rate.Count);
                return StatusCode(StatusCodes.Status429TooManyRequests, new { message = "请求过于频繁", limit = rate.Limit, window = (int)rate.Window.TotalSeconds });
            }

            if (!_publicSettingLookup.IsAnySectionEnabled(targetId))
            {
                return NotFound(new { message = "公开资源未启用" });
            }

            var timestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var password = PublicPasswordHelper.GenerateChallenge(targetId, _publicAccessOptions.SecretKey, timestampMs);
            var expiresAt = timestampMs + 5 * 60 * 1000;
            return Ok(new { password, expiresAt });
        }

        [Route("WeChat/AppId"), HttpGet]
        public IActionResult GetWeChatAppId()
        {
            var appId = GetWeChatAppIdFromConfiguration();
            return string.IsNullOrWhiteSpace(appId)
                ? BadRequest(new { message = "微信 AppId 未配置" })
                : Ok(new { appId });
        }
                
        /// <summary>
        /// 获取微信用户信息
        /// </summary>
        [Route("WeChat/UserInfo"), HttpPost]
        public async Task<IActionResult> GetWeChatUserInfo([FromBody] WeChatUserInfoRequest? request)
        {
            if (request == null)
            {
                return BadRequest(new { message = "请求不能为空" });
            }

            var appId = GetWeChatAppIdFromConfiguration();
            var secret = _configuration["WeChat:Secret"] ?? _configuration["Wechat:Secret"] ?? "";
            if (string.IsNullOrWhiteSpace(appId))
            {
                return BadRequest(new { message = "微信 AppId 未配置" });
            }

            if (string.IsNullOrWhiteSpace(request.RefreshToken) &&
                (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(secret)))
            {
                return BadRequest(new { message = "缺少 code 或微信 Secret 未配置" });
            }

            try
            {
                var token = await GetWeChatAccessTokenAsync(request, appId, secret);
                if (token == null || !string.IsNullOrEmpty(token.errcode))
                {
                    return BadRequest(new { message = token?.errmsg ?? "获取微信 AccessToken 失败" });
                }

                if (request.ScopeType == 1)
                {
                    return Ok(new { token.openid, token.access_token, token.expires_in, token.refresh_token, token.scope });
                }

                return Ok(await GetWeChatUserInfoAsync(token));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "查询微信用户信息失败");
                return BadRequest(new { message = "查询微信用户信息失败" });
            }
        }

        private string GetWeChatAppIdFromConfiguration()
        {
            return _configuration["WeChat:AppId"] ?? _configuration["Wechat:AppId"] ?? "";
        }

        private static async Task<WeChatAccessToken?> GetWeChatAccessTokenAsync(WeChatUserInfoRequest request, string appId, string secret)
        {
            string resource;
            if (!string.IsNullOrEmpty(request.RefreshToken))
            {
                resource = $"sns/oauth2/refresh_token?appid={Uri.EscapeDataString(appId)}&grant_type=refresh_token&refresh_token={Uri.EscapeDataString(request.RefreshToken)}";
            }
            else
            {
                resource = $"sns/oauth2/access_token?appid={Uri.EscapeDataString(appId)}&secret={Uri.EscapeDataString(secret)}&code={Uri.EscapeDataString(request.Code)}&grant_type=authorization_code";
            }

            using var client = new RestClient(new RestClientOptions("https://api.weixin.qq.com"));
            return await client.GetAsync<WeChatAccessToken>(new RestRequest(resource));
        }

        private static async Task<object> GetWeChatUserInfoAsync(WeChatAccessToken token)
        {
            var resource = $"sns/userinfo?access_token={Uri.EscapeDataString(token.access_token)}&openid={Uri.EscapeDataString(token.openid)}&lang=zh_CN";
            using var client = new RestClient(new RestClientOptions("https://api.weixin.qq.com"));
            var userInfo = await client.GetAsync<WeChatUserInfo>(new RestRequest(resource));
            if (userInfo != null && string.IsNullOrEmpty(userInfo.errcode))
            {
                return new
                {
                    token.openid,
                    token.access_token,
                    token.expires_in,
                    token.refresh_token,
                    token.scope,
                    userInfo.nickname,
                    userInfo.headimgurl
                };
            }
            else
            {
                return new { token.openid, message = userInfo?.errmsg ?? "获取微信用户信息失败" };
            }
        }

        public class WeChatUserInfoRequest
        {
            public string Code { get; set; } = string.Empty;
            public string RefreshToken { get; set; } = string.Empty;
            public int ScopeType { get; set; }
        }

        class WeChatAccessToken
        {
            public string errcode { get; set; } = string.Empty;
            public string errmsg { get; set; } = string.Empty;
            public string openid { get; set; } = string.Empty;
            public string access_token { get; set; } = string.Empty;
            public string refresh_token { get; set; } = string.Empty;
            public int expires_in { get; set; }
            public string scope { get; set; } = string.Empty;
        }

        class WeChatUserInfo
        {
            public string errcode { get; set; } = string.Empty;
            public string errmsg { get; set; } = string.Empty;
            public string openid { get; set; } = string.Empty;
            public string unionid { get; set; } = string.Empty;
            public string nickname { get; set; } = string.Empty;
            public string headimgurl { get; set; } = string.Empty;
            public string country { get; set; } = string.Empty;
            public string province { get; set; } = string.Empty;
            public string city { get; set; } = string.Empty;
            public int sex { get; set; }
        }
    }
}
