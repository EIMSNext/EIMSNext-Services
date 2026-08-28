using OpenIddict.Abstractions;

namespace EIMSNext.Identity.Models
{
    public sealed record PublicTokenValidationResult(
        PublicTokenSubject? Subject,
        string? Error,
        string? ErrorDescription)
    {
        public bool Succeeded => Subject != null;

        public static PublicTokenValidationResult Success(PublicTokenSubject subject) =>
            new(subject, null, null);

        public static PublicTokenValidationResult Invalid(string description) =>
            new(null, OpenIddictConstants.Errors.InvalidGrant, description);
    }
}
