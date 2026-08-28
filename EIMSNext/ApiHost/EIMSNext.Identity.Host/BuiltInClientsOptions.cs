using EIMSNext.Entities;

namespace EIMSNext.Identity.Host
{
    public sealed class BuiltInClientsOptions
    {
        public const string SectionName = "BuiltInClients";

        public BuiltInClientPolicyOptions Web { get; set; } = new()
        {
            RequireOrigin = true,
            AllowMissingOriginInDevelopment = true
        };

        public BuiltInClientPolicyOptions Public { get; set; } = new()
        {
            RequireOrigin = false
        };
    }

    public sealed class BuiltInClientPolicyOptions
    {
        public List<string> AllowedOrigins { get; set; } = [];

        public bool RequireOrigin { get; set; }

        public bool AllowMissingOriginInDevelopment { get; set; }
    }
}
