namespace EIMSNext.Auth.Models
{
    public sealed record PublicTokenSubject(string TargetId, string CorpId, string AppId, string Name);
}
