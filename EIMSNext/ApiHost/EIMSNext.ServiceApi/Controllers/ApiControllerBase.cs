using Asp.Versioning;

using EIMSNext.Common;
using EIMSNext.Core.Entity;
using EIMSNext.Core.Query;
using EIMSNext.ServiceApi.Authorization;

using HKH.Mef2.Integration;

using Microsoft.AspNetCore.Mvc;

using MongoDB.Driver;

namespace EIMSNext.ServiceApi.Controllers
{
    [ApiVersion(1.0)] public class ApiControllerBase<T, Q>(IResolver resolver) : MefControllerBase<T, Q>(resolver)
        where T : class, IEntity
        where Q : T, new()
    {
        #region DynamicQuery

        /// <summary>
        /// 动态查询数据
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        [HttpPost("dynamic/query")]
        [Permission(Operation = Operation.Read)]
        public ActionResult GetDynamicData([FromBody] DynamicFindOptions<T> options)
        {
            //TODO: fill field type
            var result = ApiService.Find(FilterResult(options)).ToList();
            return Ok(new { value = result });
        }

        /// <summary>
        /// 动态查询总数
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        [HttpPost("dynamic/count")]
        [Permission(Operation = Operation.Read)]
        public ActionResult GetDynamicCount([FromBody] DynamicFilter filter)
        {
            //TODO: fill field type
            return Ok(ApiService.Count(filter));
        }

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
            var filter = query.Filter;
            if (filter == null) { filter = new DynamicFilter(); }
            if (filter.IsGroup && filter.Rel == FilterRel.And)
            {
                filter.Items!.Add(new DynamicFilter() { Field = Fields.DeleteFlag, Op = FilterOp.Ne, Value = true });
            }
            else
            {
                filter = new DynamicFilter() { Rel = FilterRel.And, Items = [new DynamicFilter() { Field = Fields.DeleteFlag, Op = FilterOp.Ne, Value = true }, filter] };
            }

            query.Filter = filter;
            return query;
        }
        protected DynamicFindOptions<T> FilterByCorpId(DynamicFindOptions<T> query)
        {
            var filter = query.Filter;
            if (filter == null) { filter = new DynamicFilter(); }
            if (filter.IsGroup && filter.Rel == FilterRel.And)
            {
                filter.Items!.Add(new DynamicFilter() { Field = Fields.CorpId, Op = FilterOp.Eq, Value = IdentityContext.CurrentCorpId });
            }
            else
            {
                filter = new DynamicFilter() { Rel = FilterRel.And, Items = [new DynamicFilter() { Field = Fields.CorpId, Op = FilterOp.Eq, Value = IdentityContext.CurrentCorpId }, filter] };
            }

            query.Filter = filter;
            return query;
        }
        protected virtual DynamicFindOptions<T> FilterByPermission(DynamicFindOptions<T> query)
        {
            return query;
        }

        #endregion
    }
}
