using Microsoft.AspNetCore.Http;

namespace EIMSNext.ApiCore
{
    /// <summary>
    /// 跨域配置
    /// </summary>
    public class CorsMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly CorsPolicyHelper _corsPolicy;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="next"></param>
        /// <param name="corsPolicy"></param>
        public CorsMiddleware(RequestDelegate next, CorsPolicyHelper corsPolicy)
        {
            _next = next;
            _corsPolicy = corsPolicy;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task Invoke(HttpContext context)
        {
            var corsApplied = _corsPolicy.Apply(context);
            if (corsApplied && context.Request.Method.Equals(HttpMethod.Options.ToString()))
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return;
            }

            await _next(context);
        }
    }
}
