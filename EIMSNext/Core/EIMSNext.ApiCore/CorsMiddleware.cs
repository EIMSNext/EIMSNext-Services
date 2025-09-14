using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace EIMSNext.ApiCore
{
    /// <summary>
    /// 跨域配置
    /// </summary>
    public class CorsMiddleware
    {
        private readonly RequestDelegate _next;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="next"></param>
        public CorsMiddleware(RequestDelegate next)
        {
            _next = next;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task Invoke(HttpContext context)
        {
            if (context.Request.Headers.ContainsKey(CorsConstants.Origin))
            {
                context.Response.Headers.AccessControlAllowOrigin = context.Request.Headers.Origin;
                context.Response.Headers.AccessControlAllowMethods = "PUT,POST,GET,DELETE,OPTIONS,HEAD,PATCH";
                context.Response.Headers.AccessControlAllowHeaders = context.Request.Headers.AccessControlRequestHeaders;
                context.Response.Headers.AccessControlAllowCredentials = "true";

                if (context.Request.Method.Equals(HttpMethod.Options.ToString()))
                {
                    context.Response.StatusCode = StatusCodes.Status200OK;
                    return;
                }
            }

            await _next(context);
        }
    }
}
