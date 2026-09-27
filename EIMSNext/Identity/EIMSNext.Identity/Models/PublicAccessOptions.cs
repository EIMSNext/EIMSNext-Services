namespace EIMSNext.Identity.Models
{
    public sealed class PublicAccessOptions
    {
        public const string SectionName = "PublicAccess";

        public string SecretKey { get; set; } = string.Empty;
    }
}
