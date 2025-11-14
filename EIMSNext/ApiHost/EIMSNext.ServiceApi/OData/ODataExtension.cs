using HKH.Common;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Query.Validator;

namespace EIMSNext.ServiceApi.OData
{
    /// <summary>
    /// 
    /// </summary>
    public static class ODataExtension
    {
        private static ODataValidationSettings validationSettings = new ODataValidationSettings() { MaxExpansionDepth = 3, MaxNodeCount = 200 };
        private static ODataQuerySettings querySettings = new ODataQuerySettings();

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="opts"></param>
        /// <param name="queryable"></param>
        /// <param name="count"></param>
        /// <param name="setting"></param>
        /// <returns></returns>
        //public static ApiResult ApplyTo<T>(this ODataQueryOptions<T> opts, IQueryable<T> queryable, bool count, ODataQuerySettings? setting = null)
        //{
        //    try
        //    {
        //        opts.Validate(validationSettings);
        //        setting = setting ?? querySettings;

        //        var pageData = new PageData<T>();
        //        if (count)
        //        {
        //            var filterredQuery = (opts.Filter == null ? queryable : opts.Filter.ApplyTo(queryable, setting));
        //            pageData.Results = opts.ApplyTo(filterredQuery, setting);
        //            pageData.Total = filterredQuery.OfType<T>().Count();
        //        }
        //        else
        //        {
        //            pageData.Results = opts.ApplyTo(queryable, setting);
        //        }

        //        return ApiResult.Page(pageData);
        //    }
        //    catch (ODataException ex)
        //    {
        //        return ApiResult.Fail(-1, ex.Message);
        //    }
        //}

        /// <summary>
        /// 读取参数值
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="keyValues"></param>
        /// <param name="parameterName"></param>
        /// <returns></returns>
        public static T? GetParameterValue<T>(this ODataActionParameters keyValues, string parameterName)
        {
            if (keyValues == null) throw new UnLogException("传入的参数名称或类型不匹配");
            if (!keyValues.ContainsKey(parameterName)) throw new UnLogException($"缺少参数:{parameterName}");

            return (T)keyValues[parameterName];
        }
    }
}
