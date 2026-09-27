namespace EIMSNext.Entities;

/// <summary>
/// Minimal projection of the shared Employee collection for SSO lookup.
/// </summary>
public sealed class EmployeeLookup
{
    public string Id { get; set; } = string.Empty;

    public string CorpId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public int Status { get; set; }
}
