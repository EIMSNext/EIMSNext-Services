namespace EIMSNext.Auth.Models
{
    public sealed class SystemTaskTokenRequest
    {
        public string CorpId { get; set; } = string.Empty;

        public string ObjectType { get; set; } = string.Empty;

        public string ObjectId { get; set; } = string.Empty;
    }
}
