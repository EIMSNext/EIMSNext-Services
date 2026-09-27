using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Entities;

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
        public string? PermissionGroupId { get; set; }

        /// <summary>最多返回的选项数量，默认 50。</summary>
        public int Limit { get; set; } = 50;
    }

    /// <summary>
    /// 动态表单字段选项查询响应。
    /// </summary>
    public class FormDataFilterOptionsResponse
    {
        /// <summary>过滤选项列表。</summary>
        public List<FilterOptionItem> Items { get; set; } = [];
    }

    /// <summary>
    /// 表单数据权限范围响应。
    /// </summary>
    public class FormDataPermissionScopeResponse
    {
        /// <summary>表单数据权限。</summary>
        public FormDataPermissions FormDataPermissions { get; set; }

        /// <summary>表单字段权限列表。</summary>
        public List<FormFieldPermission>? FormFieldPermissions { get; set; }
    }
}
