using Asp.Versioning;
using EIMSNext.ApiService;
using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;

using HKH.Mef2.Integration;

namespace EIMSNext.Service.Host.Controllers
{
    [ApiVersion(1.0)]
    public class ApiControllerBase<S, T, Q> : MefControllerBase<S, T, Q>
       where S : class, IApiService<T, Q>
        where T : class, IEntity
        where Q : T, new()
    {
        public ApiControllerBase(IResolver resolver) : base(resolver)
        {
        }

        protected string? QueryAppId => Request.Query.FirstOrDefault(x => x.Key.EqualsIgnoreCase("appid")).Value;
        protected string? QueryFormId => Request.Query.FirstOrDefault(x => x.Key.EqualsIgnoreCase("formid")).Value;

        #region DynamicQuery

        ///// <summary>
        ///// 动态查询数据
        ///// </summary>
        ///// <param name="options"></param>
        ///// <returns></returns>
        //[HttpPost("dynamic/$query")]
        //[Permission(Operation = Operation.Read)]
        //public ActionResult GetDynamicData([FromBody] DynamicFindOptions<T> options)
        //{
        //    //TODO: fill field type
        //    var result = ApiService.Find(FilterResult(options)).ToList();
        //    return Ok(new { value = result });
        //}

        ///// <summary>
        ///// 动态查询总数
        ///// </summary>
        ///// <param name="filter"></param>
        ///// <returns></returns>
        //[HttpPost("dynamic/$count")]
        //[Permission(Operation = Operation.Read)]
        //public ActionResult GetDynamicCount([FromBody] DynamicFilter filter)
        //{
        //    //TODO: fill field type
        //    return Ok(ApiService.Count(filter));
        //}

        /// <summary>
        /// 对按请求的上下文进行数据过滤，比如用户只能访问被授权的数据
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        protected virtual DynamicFindOptions<T> FilterResult(DynamicFindOptions<T> query)
        {
            return FilterByPermission(FilterByDeleted(FilterByCorpId(query)));
        }
        protected DynamicFindOptions<T> FilterByDeleted(DynamicFindOptions<T> query)
        {
            query.Filter = query.Filter.And(Fields.DeleteFlag, FilterOp.Ne, true);
            return query;
        }
        protected DynamicFindOptions<T> FilterByCorpId(DynamicFindOptions<T> query)
        {
            query.Filter = query.Filter.And(Fields.CorpId, FilterOp.Eq, IdentityContext.CurrentCorpId);
            return query;
        }
        protected virtual DynamicFindOptions<T> FilterByPermission(DynamicFindOptions<T> query)
        {
            return query;
        }

        #endregion
    }
}
