namespace EIMSNext.Auth.Entity
{
    public abstract class UserClaim
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; }=string.Empty;
    }
}