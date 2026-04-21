using EIMSNext.Core.Entities;

namespace EIMSNext.Auth.Entities
{
    public class Client : MongoEntityBase
    {
        public string CorpId { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public List<ClientSecret> ClientSecrets { get; set; } = [];
        public bool RequireClientSecret { get; set; } = true;
        public string? ClientName { get; set; }
        public List<ClientGrantType> AllowedGrantTypes { get; set; } = new List<ClientGrantType>();
        public List<ClientScope> AllowedScopes { get; set; } = [];
        public int IdentityTokenLifetime { get; set; } = 28800;
        public int AccessTokenLifetime { get; set; } = 28800;
        public string ApiKey { get; set; } = string.Empty;
    }
}
