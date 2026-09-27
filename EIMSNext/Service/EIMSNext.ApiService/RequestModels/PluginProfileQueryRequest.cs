namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 插件档案查询请求。
    /// </summary>
    public class PluginProfileQueryRequest
    {
        /// <summary>关键字。</summary>
        public string? Keyword { get; set; }

        /// <summary>分类。</summary>
        public string? Category { get; set; }

        /// <summary>场景。</summary>
        public string? Scenario { get; set; }

        /// <summary>是否推荐。</summary>
        public bool? Recommended { get; set; }

        /// <summary>跳过的记录数。</summary>
        public int Skip { get; set; } = 0;

        /// <summary>返回的记录数。</summary>
        public int Take { get; set; } = 24;
    }
}
