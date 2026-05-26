using EIMSNext.Auth.Entities;

namespace EIMSNext.Auth.Models
{
    public sealed class IntegrationValidationResult
    {
        public User? User { get; init; }

        public string FailureMessage { get; init; } = string.Empty;

        public bool Succeeded => User != null;
    }
}
