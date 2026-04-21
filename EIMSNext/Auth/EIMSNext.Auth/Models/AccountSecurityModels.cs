namespace EIMSNext.Auth.AccountSecurity
{
    public static class PinCodeUsage
    {
        public const string Verify = "verify";
        public const string Bind = "bind";
    }

    public static class PinCodeTargetType
    {
        public const string Phone = "phone";
        public const string Email = "email";
    }

    public static class VerifyIdentityType
    {
        public const string Password = "password";
        public const string Phone = "phone";
        public const string Email = "email";
    }

    public class SendPinCodeRequest
    {
        public string Type { get; set; } = "";
        public string Usage { get; set; } = "";
        public string Target { get; set; } = "";
    }

    public class VerifyIdentityRequest
    {
        public string Type { get; set; } = "";
        public string? Password { get; set; }
        public string? Code { get; set; }
    }

    public class VerifyIdentityResponse
    {
        public string VerifyToken { get; set; } = "";
        public DateTime ExpireAt { get; set; }
    }

    public class ChangePasswordRequest
    {
        public string VerifyToken { get; set; } = "";
        public string NewPassword { get; set; } = "";
        public string ConfirmPassword { get; set; } = "";
    }

    public class ChangePhoneRequest
    {
        public string VerifyToken { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Code { get; set; } = "";
    }

    public class ChangeEmailRequest
    {
        public string VerifyToken { get; set; } = "";
        public string Email { get; set; } = "";
        public string Code { get; set; } = "";
    }

    public class UnbindPhoneRequest
    {
        public string VerifyToken { get; set; } = "";
    }

    public class UnbindEmailRequest
    {
        public string VerifyToken { get; set; } = "";
    }

    internal class VerifyIdentityTicket
    {
        public string UserId { get; set; } = "";
        public DateTime VerifiedAt { get; set; }
    }
}
