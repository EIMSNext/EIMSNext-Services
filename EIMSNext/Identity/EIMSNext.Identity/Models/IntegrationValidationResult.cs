using EIMSNext.Entities;

namespace EIMSNext.Identity.Models
{
    public sealed class IntegrationValidationResult
    {
        public User? User { get; init; }

        public string FailureMessage { get; init; } = string.Empty;

        public bool Succeeded => User != null;
    }
}
