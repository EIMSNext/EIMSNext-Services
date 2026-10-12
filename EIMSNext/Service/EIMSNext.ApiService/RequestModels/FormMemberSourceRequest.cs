using EIMSNext.Core.Query;

namespace EIMSNext.ApiService.RequestModels;

/// <summary>
/// 表单员工/部门数据源查询请求。
/// </summary>
public sealed class FormMemberSourceRequest
{
    /// <summary>表单 ID。</summary>
    public string? FormId { get; set; }
    /// <summary>字段 ID，子表字段使用路径格式。</summary>
    public string? FieldId { get; set; }
    /// <summary>数据源类型：employee 或 department。</summary>
    public string SourceType { get; set; } = string.Empty;
    /// <summary>是否为表单设计态请求。</summary>
    public bool Design { get; set; }
    /// <summary>安全过滤条件。</summary>
    public DynamicFilter? Filter { get; set; }
    /// <summary>安全排序条件。</summary>
    public DynamicSortList? Sort { get; set; }
    /// <summary>跳过记录数。</summary>
    public int Skip { get; set; }
    /// <summary>请求记录数。</summary>
    public int Take { get; set; }
    /// <summary>搜索关键字。</summary>
    public string? Keyword { get; set; }
    /// <summary>关键字搜索字段。</summary>
    public List<string>? SearchFields { get; set; }
    /// <summary>员工查询的部门 ID。</summary>
    public string? DepartmentId { get; set; }
    /// <summary>员工查询是否包含部门下级。</summary>
    public bool DepartmentCascaded { get; set; }
}
