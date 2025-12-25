using System.Globalization;

using EIMSNext.Common;

using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.ApiHost.Extension
{
    /// <summary>
    /// 
    /// </summary>
    public static class ApiResultExtension
    {
        /// <summary>
        /// 转为类似odata result的结构，统一格式
        /// </summary>
        /// <param name="apiResult"></param>
        /// <returns></returns>
        public static ActionResult ToActionResult(this ApiResult apiResult)
        {
            if (apiResult.Code.HasValue && apiResult.Code.Value != 0)
            {
                return new ErrorResult(apiResult.Code.Value, apiResult.Message ?? string.Empty);
            }
            else
            {
                return new ObjectResult(apiResult.Value);
            }
        }
    }

    public class ErrorResult : ActionResult
    {
        public Error Error { get; }

        public ErrorResult(int errorCode, string message)
        {
            Error = new Error
            {
                ErrorCode = errorCode,
                Message = message
            };
        }

        public override async Task ExecuteResultAsync(ActionContext context)
        {
            ObjectResult objectResult = new ObjectResult(Error)
            {
                StatusCode = Convert.ToInt32(Error.ErrorCode, CultureInfo.InvariantCulture)
            };
            await objectResult.ExecuteResultAsync(context).ConfigureAwait(continueOnCapturedContext: false);
        }
    }

    public class Error
    {
        public int ErrorCode { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
