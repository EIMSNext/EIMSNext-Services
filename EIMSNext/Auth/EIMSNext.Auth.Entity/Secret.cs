using IdentityServer4;

namespace EIMSNext.Auth.Entity
{
    public abstract class Secret
    {
        public string? Description { get; set; }
        public string? Value { get; set; }
        public DateTime? Expiration { get; set; }
        public string Type { get; set; } = IdentityServerConstants.SecretTypes.SharedSecret;
    }
}