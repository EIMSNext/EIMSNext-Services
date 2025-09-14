namespace EIMSNext.Auth.Options
{
    public class TokenCleanupOptions
    {
        public int Interval { get; set; } = 60;
        public bool Enable { get; set; }
    }
}