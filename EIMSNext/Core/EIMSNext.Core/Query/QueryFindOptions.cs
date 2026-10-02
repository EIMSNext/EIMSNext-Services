using System.Linq.Expressions;
using EIMSNext.Core.Query;

namespace EIMSNext.Core.Query
{
    /// <summary>
    /// 通用查询选项，包含过滤、排序、分页配置。
    /// </summary>
    /// <typeparam name="T">实体类型。</typeparam>
    public class QueryFindOptions<T>
    {
        /// <summary>
        /// 初始化 <see cref="QueryFindOptions{T}"/> 类的新实例。
        /// </summary>
        public QueryFindOptions()
        {
        }

        /// <summary>
        /// 使用过滤谓词初始化 <see cref="QueryFindOptions{T}"/> 类的新实例。
        /// </summary>
        public QueryFindOptions(Expression<Func<T, bool>> filter) => Filter = filter;

        /// <summary>
        /// 获取或设置过滤谓词。为 null 时表示不过滤。
        /// </summary>
        public Expression<Func<T, bool>>? Filter { get; set; }

        /// <summary>
        /// 获取或设置排序定义。支持字符串字段名的动态排序。
        /// </summary>
        public DynamicSortDefinition? Sort { get; set; }

        /// <summary>
        /// 获取或设置跳过的记录数。
        /// </summary>
        public int Skip { get; set; }

        /// <summary>
        /// 获取或设置返回的记录数。0（或负数）表示不限量，由调用方自行控制结果规模。
        /// </summary>
        /// <remarks>
        /// 请求入口不得直接使用这个默认值 —— 客户端不传分页参数时会退化成整表拉取。
        /// 来自请求的实例必须先过 <see cref="RequestPagingPolicy.Normalize"/>。
        /// </remarks>
        public int Take { get; set; }

        /// <summary>
        /// 是否包含逻辑删除数据。为 true 时跳过模型层挂的全局 <c>!DeleteFlag</c> 查询过滤，
        /// 使回收站查询能真正读到已软删除的行。
        /// </summary>
        public bool IncludeDeleted { get; set; }

        /// <summary>
        /// 获取有效的返回记录数。
        /// </summary>
        /// <returns>负数归一化为 0；0 表示不限量，不会生成 LIMIT。</returns>
        public int GetEffectiveTake()
        {
            return Math.Max(0, Take);
        }

        /// <summary>
        /// 获取有效的跳过记录数。
        /// </summary>
        /// <returns>有效的跳过记录数。</returns>
        public int GetEffectiveSkip()
        {
            return Math.Max(0, Skip);
        }
    }
}
