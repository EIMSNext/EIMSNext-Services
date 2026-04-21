using System.Security.Claims;

namespace EIMSNext.Auth.Models
{
    public sealed class TokenRequestResult
    {
        public bool Succeeded { get; init; }
        public string? Error { get; init; }
        public string? ErrorDescription { get; init; }
        public string? Subject { get; init; }
        public string? AuthenticationMethod { get; init; }
        public int AccessTokenLifetime { get; init; }
        public IReadOnlyList<string> Scopes { get; init; } = Array.Empty<string>();
        public IReadOnlyList<Claim> Claims { get; init; } = Array.Empty<Claim>();

        public static TokenRequestResult Success(string? subject, string? authenticationMethod, int accessTokenLifetime, IReadOnlyList<string> scopes, IReadOnlyList<Claim> claims)
        {
            return new TokenRequestResult
            {
                Succeeded = true,
                Subject = subject,
                AuthenticationMethod = authenticationMethod,
                AccessTokenLifetime = accessTokenLifetime,
                Scopes = scopes,
                Claims = claims
            };
        }

        public static TokenRequestResult Failure(string error, string description)
        {
            return new TokenRequestResult
            {
                Succeeded = false,
                Error = error,
                ErrorDescription = description
            };
        }
    }
}
