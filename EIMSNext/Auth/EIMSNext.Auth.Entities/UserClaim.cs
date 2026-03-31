namespace EIMSNext.Auth.Entities
{
    public abstract class UserClaim
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; }=string.Empty;
    }
}