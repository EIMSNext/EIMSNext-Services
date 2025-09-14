using Microsoft.Net.Http.Headers;

namespace EIMSNext.ServiceApi.OData
{
    /// <summary>
    /// OData元数据默认不返回，但是请求时可指定返回
    /// </summary>
    public class ODataMetadataMiddleware
    {
        private readonly RequestDelegate _next;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="next"></param>
        public ODataMetadataMiddleware(RequestDelegate next)
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
            if (context.Request.Path.StartsWithSegments("/odata") && context.Request.Headers.ContainsKey(HeaderNames.Accept))
            {
                var accept = context.Request.Headers[HeaderNames.Accept].ToString();
                if (!accept.Contains("odata.metadata"))
                {
                    if (accept == "*/*")
                        context.Request.Headers[HeaderNames.Accept] = $"application/json;odata.metadata=none";
                    else
                        context.Request.Headers[HeaderNames.Accept] = $"{accept.TrimEnd(';')};odata.metadata=none";
                }
            }

            await _next(context);
        }
    }
}
