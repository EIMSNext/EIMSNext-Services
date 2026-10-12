namespace EIMSNext.ApiService.ViewModels;

/// <summary>
/// 表单成员数据源的轻量返回项。
/// </summary>
public sealed class FormMemberSourceViewModel
{
    /// <summary>实体 ID。</summary>
    public string Id { get; init; } = string.Empty;
    /// <summary>实体编码。</summary>
    public string Code { get; init; } = string.Empty;
    /// <summary>显示名称。</summary>
    public string Label { get; init; } = string.Empty;
    /// <summary>员工状态。</summary>
    public int? Status { get; init; }
    /// <summary>返回项类型。</summary>
    public string Type { get; init; } = string.Empty;
    /// <summary>部门父级 ID。</summary>
    public string? ParentId { get; init; }
    /// <summary>部门层级路径。</summary>
    public string? HeriarchyId { get; init; }
}
