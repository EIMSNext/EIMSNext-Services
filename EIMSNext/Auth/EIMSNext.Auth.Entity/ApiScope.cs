using EIMSNext.Core.Entity;

namespace EIMSNext.Auth.Entity
{
    public class ApiScope : MongoEntityBase
    {
        public string? Name { get; set; }
        public string? DisplayName { get; set; }
        public string? Description { get; set; }
        public bool Required { get; set; }
        public bool Emphasize { get; set; }
        public bool ShowInDiscoveryDocument { get; set; } = true;
        public List<ApiScopeClaim>? UserClaims { get; set; }
    }
}