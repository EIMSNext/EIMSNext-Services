using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.AuthApi.Controllers
{
    /// <summary>
    /// 所有方法都允许匿名访问，无需登录
    /// </summary>
    [ApiController]
    public class OpenController : ControllerBase
    {
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
    }
}
