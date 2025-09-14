using EIMSNext.Core.Entity;

namespace EIMSNext.Auth.Entity
{
    public class ApiResource : MongoEntityBase
    {
        public bool Enabled { get; set; } = true;
        public string Name { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string? Description { get; set; }
        public IDictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();

        public List<ApiSecret> Secrets { get; set; } = new List<ApiSecret>();
        public List<string> Scopes { get; set; } = new List<string>();
        public List<ApiResourceClaim> UserClaims { get; set; } = new List<ApiResourceClaim> { };
    }
}