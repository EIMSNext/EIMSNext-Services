using Asp.Versioning;
using EIMSNext.ApiService;
using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Query;

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

        /// <summary>
        /// 查询过滤链：企业隔离 → 软删除 → 权限。
        /// </summary>
        protected virtual DynamicFindOptions<T> FilterResult(DynamicFindOptions<T> query)
        {
            query = FilterByCorpId(query);
            if (!query.IncludeDeleted)
            {
                query = FilterByDeleted(query);
            }

            return FilterByPermission(query);
        }

        /// <summary>
        /// 按当前企业过滤。
        /// </summary>
        protected virtual DynamicFindOptions<T> FilterByCorpId(DynamicFindOptions<T> query)
        {
            query.Filter = query.Filter.And(Fields.CorpId, FilterOp.Eq, IdentityContext.CurrentCorpId);
            return query;
        }

        /// <summary>
        /// 排除软删除数据。
        /// </summary>
        protected virtual DynamicFindOptions<T> FilterByDeleted(DynamicFindOptions<T> query)
        {
            query.Filter = query.Filter.And(Fields.DeleteFlag, FilterOp.Ne, true);
            return query;
        }

        /// <summary>
        /// 数据权限过滤。默认不过滤，由子控制器按需覆写。
        /// </summary>
        protected virtual DynamicFindOptions<T> FilterByPermission(DynamicFindOptions<T> query)
        {
            return query;
        }
    }
}
