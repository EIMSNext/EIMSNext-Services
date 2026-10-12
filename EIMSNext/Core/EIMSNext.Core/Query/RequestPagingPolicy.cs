namespace EIMSNext.Core.Query
{
    /// <summary>
    /// 来自外部请求（HTTP / 公开链接）的分页上限策略。
    /// </summary>
    /// <remarks>
    /// 仓储层的 <see cref="QueryFindOptions{T}.Take"/> / <see cref="DynamicFindOptions{T}.Take"/>
    /// 默认 0 表示不限量，方便服务端内部按业务需要取全量；但请求入口不能沿用这个语义 ——
    /// 客户端不传分页参数时若按不限量执行，一次请求就能把整表拉进内存。
    /// 因此凡是从请求反序列化得到的查询选项，必须在进入服务层前过一次
    /// <see cref="Normalize"/>：未指定按 <see cref="DefaultTake"/> 取，指定则截断到上限。
    /// </remarks>
    public static class RequestPagingPolicy
    {
        /// <summary>请求未指定单页数量时使用的默认值。</summary>
        public const int DefaultTake = 20;

        /// <summary>请求允许的单页数量上限。</summary>
        public const int MaxTake = 1000;

        /// <summary>公开访问（公示查询 / 公开表单链接）的单页数量上限，比登录态更严格。</summary>
        public const int PublicMaxTake = 200;

        /// <summary>
        /// 把请求侧的单页数量归一化到 [1, maxTake]。
        /// </summary>
        /// <param name="take">请求传入的单页数量。</param>
        /// <param name="maxTake">上限，默认 <see cref="MaxTake"/>。</param>
        /// <returns>非正数返回 <see cref="DefaultTake"/>；否则返回与上限的较小值。</returns>
        public static int Normalize(int take, int maxTake = MaxTake)
        {
            var ceiling = maxTake <= 0 ? MaxTake : maxTake;
            return take <= 0 ? DefaultTake : Math.Min(take, ceiling);
        }
    }
}
