using EIMSNext.Core.Entity;

namespace EIMSNext.Auth.Entity
{
    public class IdentityResource : MongoEntityBase
    {
        public bool Enabled { get; set; } = true;
        public string Name { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string? Description { get; set; }
        public bool Required { get; set; }
        public bool Emphasize { get; set; }
        public bool ShowInDiscoveryDocument { get; set; } = true;
        public List<IdentityClaim> UserClaims { get; set; } = new List<IdentityClaim> { };
        public IDictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
    }
}