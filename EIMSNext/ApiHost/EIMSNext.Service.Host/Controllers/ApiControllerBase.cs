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
            query = NormalizePaging(query);
            query = FilterByCorpId(query);
            if (!query.IncludeDeleted)
            {
                query = FilterByDeleted(query);
            }

            return FilterByPermission(query);
        }

        /// <summary>
        /// 收敛请求侧分页参数。
        /// </summary>
        /// <remarks>
        /// 仓储层的 <c>Take</c> 默认 0 表示不限量，那是给服务端内部代码用的；
        /// 请求入口若沿用这个语义，客户端不传分页参数就会退化成整表拉取。
        /// 因此所有由请求反序列化得到的 <see cref="DynamicFindOptions{T}"/> 都必须先过这里。
        /// 覆写 <see cref="FilterResult"/> 时必须自行调用本方法。
        /// </remarks>
        protected static DynamicFindOptions<T> NormalizePaging(DynamicFindOptions<T> query)
        {
            query.Take = RequestPagingPolicy.Normalize(query.Take);
            query.Skip = query.GetEffectiveSkip();
            return query;
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
