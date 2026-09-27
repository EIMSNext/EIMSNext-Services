namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 应用档案查询请求。
    /// </summary>
    public class AppProfileQueryRequest
    {
        /// <summary>关键字。</summary>
        public string? Keyword { get; set; }

        /// <summary>分类。</summary>
        public string? Category { get; set; }

        /// <summary>行业。</summary>
        public string? Industry { get; set; }

        /// <summary>是否推荐。</summary>
        public bool? Recommended { get; set; }

        /// <summary>跳过的记录数。</summary>
        public int Skip { get; set; } = 0;

        /// <summary>返回的记录数。</summary>
        public int Take { get; set; } = 24;
    }
}
