using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Service.Entities;

namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 动态表单字段选项查询请求。
    /// </summary>
    public class FormDataFilterOptionsRequest
    {
        /// <summary>目标表单 ID。</summary>
        public string FormId { get; set; } = string.Empty;

        /// <summary>目标字段路径。</summary>
        public string Field { get; set; } = string.Empty;

        /// <summary>字段类型，参见 FieldType 常量。</summary>
        public string? FieldType { get; set; }

        /// <summary>选项关键字。</summary>
        public string? Keyword { get; set; }

        /// <summary>附加筛选条件。</summary>
        public DynamicFilter? Filter { get; set; }

        /// <summary>数据权限组 ID。</summary>
        public string? AuthGroupId { get; set; }

        /// <summary>最多返回的选项数量，默认 50。</summary>
        public int Limit { get; set; } = 50;
    }

    public class FormDataFilterOptionsResponse
    {
        public List<FilterOptionItem> Items { get; set; } = [];
    }

    public class FormDataPermissionScopeResponse
    {
        public DataPerms DataPerms { get; set; }

        public List<FieldPerm>? FieldPerms { get; set; }
    }
}
