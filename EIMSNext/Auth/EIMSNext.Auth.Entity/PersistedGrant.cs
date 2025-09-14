using EIMSNext.Core.Entity;

namespace EIMSNext.Auth.Entity
{
    public class PersistedGrant : MongoEntityBase
    {
        public string? Type { get; set; }
        public string? UserId { get; set; }
        public string? ClientId { get; set; }
        public DateTime CreationTime { get; set; } = DateTime.UtcNow;
        public DateTime? Expiration { get; set; }
        public string? Data { get; set; }
    }
}