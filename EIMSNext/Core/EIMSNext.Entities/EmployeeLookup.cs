namespace EIMSNext.Entities;

/// <summary>
/// Minimal projection of the shared Employee collection for SSO lookup.
/// <para>
/// 迁移说明：原 <c>[BsonId]</c> / <c>[BsonElement("...")]</c> 特性已移除——
/// PostgreSQL 侧列名由 EF Core 映射决定（实体名 + 属性名，如需改列名请用 Fluent API
/// 的 <c>HasColumnName</c>），不再依赖 BSON 序列化约定。
/// </para>
/// </summary>
public sealed class EmployeeLookup
{
    public string Id { get; set; } = string.Empty;

    public string CorpId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public int Status { get; set; }
}
