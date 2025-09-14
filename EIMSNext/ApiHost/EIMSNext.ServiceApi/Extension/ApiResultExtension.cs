using EIMSNext.Common;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Results;

namespace EIMSNext.ServiceApi.Extension
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
                return new ODataErrorResult(apiResult.Code.ToString(), apiResult.Message);
            }
            else
            {
                return new ObjectResult(apiResult.Value);
            }
        }
    }
}
