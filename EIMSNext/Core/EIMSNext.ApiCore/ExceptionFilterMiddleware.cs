using System.Text.Json;
using EIMSNext.Common;
using HKH.Common;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EIMSNext.ApiCore
{
    /// <summary>
    /// 全局异常处理
    /// </summary>
    public class ExceptionFilterMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionFilterMiddleware> _logger;
        private readonly IWebHostEnvironment _environment;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="next"></param>
        /// <param name="logger"></param>
        public ExceptionFilterMiddleware(RequestDelegate next, IWebHostEnvironment environment, ILogger<ExceptionFilterMiddleware> logger)
        {
            _next = next;
            _environment = environment;
            _logger = logger;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="httpContext"></param>
        /// <returns></returns>
        public async Task Invoke(HttpContext httpContext)
        {
            try
            {
                await _next(httpContext);
            }
            catch (Exception ex)
            {
                if (!(ex is UnLogException)) _logger.LogError(ex, "Unhandled exception....");
                await HandleExceptionAsync(httpContext, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext httpContext, Exception ex)
        {
            var error = httpContext.Features.Get<IExceptionHandlerFeature>()?.Error ?? ex;

            if (error != null)
            {
                var errorInfo = GetErrorInfo(error);
                if (!httpContext.Response.Headers.IsReadOnly)
                {
                    httpContext.Response.ContentType = "application/json";
                    httpContext.Response.StatusCode = errorInfo.StatusCode;
                }

                await httpContext.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    statecode = errorInfo.StateCode,
                    message = errorInfo.Message,
                }));
            }
        }

        private (int StatusCode, string StateCode, string Message) GetErrorInfo(Exception error)
        {
            if (error is HttpException httpException)
            {
                return (httpException.StatusCode, httpException.StateCode, httpException.Message);
            }

            var errorMsg = _environment.IsDevelopment() ? GetInnerExceptionMessage(error) : "抱歉，出错了";
            return (StatusCodes.Status500InternalServerError, "internalservererror", errorMsg);
        }

        private string GetInnerExceptionMessage(Exception ex)
        {
            var msg = ex.Message;
            var innerEx = ex.InnerException;
            while (innerEx != null)
            {
                msg = innerEx.Message;
                innerEx = innerEx.InnerException;
            }
            return msg;
        }
    }
}
