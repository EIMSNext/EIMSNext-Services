using System.Text.RegularExpressions;
using EIMSNext.Auth.AccountSecurity;
using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Interfaces;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;
using Microsoft.Extensions.Caching.Memory;

namespace EIMSNext.Auth.Services
{
    public class AccountSecurityService(
        IAuthDbContext dbContext,
        IMemoryCache memoryCache,
        IVerificationCodeProvider? codeProvider = null) : IAccountSecurityService
    {
        private readonly IVerificationCodeProvider verificationCodeProvider =
            codeProvider ?? new MockVerificationCodeProvider();

        private static readonly TimeSpan VerifyTokenTtl = TimeSpan.FromMinutes(10);
        private static readonly Regex PhoneRegex = new("^1[3-9]\\d{9}$", RegexOptions.Compiled);
        private static readonly Regex EmailRegex = new(@"^\w[-\w.+]*@([A-Za-z0-9][-A-Za-z0-9]+\.)+[A-Za-z]{2,14}$", RegexOptions.Compiled);
        private static readonly Regex UppercaseRegex = new("[A-Z]", RegexOptions.Compiled);
        private static readonly Regex LowercaseRegex = new("[a-z]", RegexOptions.Compiled);
        private static readonly Regex DigitRegex = new("\\d", RegexOptions.Compiled);
        private static readonly Regex SpecialCharRegex = new("[^A-Za-z0-9]", RegexOptions.Compiled);

        public Task<VerificationCodeSendResult> SendRegCodeAsync(SendRegCodeRequest request)
        {
            if (!IsTargetType(request.Type))
            {
                throw new InvalidOperationException("验证码类型无效");
            }

            var target = NormalizeAndValidateTarget(request.Type, request.Target);
            EnsureTargetAvailable(request.Type, target, string.Empty);
            return Task.FromResult(verificationCodeProvider.Send(VerificationCodePurpose.Register, target));
        }

        public Task<VerificationCodeSendResult> SendLoginCodeAsync(SendRegCodeRequest request)
        {
            if (!IsTargetType(request.Type))
            {
                throw new InvalidOperationException("验证码类型无效");
            }

            var target = NormalizeAndValidateTarget(request.Type, request.Target);
            var userExists = request.Type == PinCodeTargetType.Phone
                ? dbContext.Users.Any(x => !x.Disabled && x.Phone == target)
                : dbContext.Users.Any(x => !x.Disabled && x.Email == target);

            return Task.FromResult(userExists
                ? verificationCodeProvider.Send(VerificationCodePurpose.Login, target)
                : new VerificationCodeSendResult(DateTimeOffset.UtcNow.AddMinutes(5), null));
        }

        public async Task RegisterAsync(RegisterRequest request)
        {
            if (!IsTargetType(request.Type))
            {
                throw new InvalidOperationException("注册类型无效");
            }

            ValidatePasswordStrength(request.Password, "密码");

            var user = new User
            {
                Password = HKH.Common.Security.BCrypt.HashPassword(request.Password),
                Platform = PlatformType.Public,
                CreateTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };

            if (request.Type == PinCodeTargetType.Phone)
            {
                var phone = NormalizeAndValidateTarget(request.Type, request.Phone);
                EnsureTargetAvailable(request.Type, phone, string.Empty);
                if (!verificationCodeProvider.TryConsume(VerificationCodePurpose.Register, phone, request.Code))
                {
                    throw new InvalidOperationException("验证码错误");
                }

                user.Phone = phone;
                user.Name = phone;
            }
            else
            {
                var email = NormalizeAndValidateTarget(request.Type, request.Email);
                EnsureTargetAvailable(request.Type, email, string.Empty);
                if (!verificationCodeProvider.TryConsume(VerificationCodePurpose.Register, email, request.Code))
                {
                    throw new InvalidOperationException("验证码错误");
                }

                user.Email = email;
                user.Name = GetEmailName(email);
            }

            await dbContext.AddUser(user);
        }

        public Task<VerificationCodeSendResult> SendPinCodeAsync(string userId, SendPinCodeRequest request)
        {
            var user = GetCurrentUser(userId);

            if (!IsTargetType(request.Type))
            {
                throw new InvalidOperationException("验证码类型无效");
            }

            if (!IsUsage(request.Usage))
            {
                throw new InvalidOperationException("验证码用途无效");
            }

            if (request.Usage == PinCodeUsage.Verify)
            {
                var target = NormalizeAndValidateTarget(request.Type, request.Target);
                if (request.Type == PinCodeTargetType.Phone)
                {
                    if (string.IsNullOrWhiteSpace(user.Phone))
                    {
                        throw new InvalidOperationException("当前账号未绑定手机");
                    }

                    if (!string.Equals(user.Phone, target, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("手机号与当前账号绑定手机号不一致");
                    }
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(user.Email))
                    {
                        throw new InvalidOperationException("当前账号未绑定邮箱");
                    }

                    if (!string.Equals(user.Email, target, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("邮箱与当前账号绑定邮箱不一致");
                    }
                }
            }
            else
            {
                var target = NormalizeAndValidateTarget(request.Type, request.Target);
                EnsureTargetAvailable(request.Type, target, user.Id);
            }

            var normalizedTarget = NormalizeAndValidateTarget(request.Type, request.Target);
            var purpose = request.Usage == PinCodeUsage.Verify
                ? VerificationCodePurpose.VerifyIdentity
                : VerificationCodePurpose.Bind;
            return Task.FromResult(verificationCodeProvider.Send(purpose, normalizedTarget));
        }

        public Task<VerifyIdentityResponse> VerifyIdentityAsync(string userId, VerifyIdentityRequest request)
        {
            var user = GetCurrentUser(userId);

            switch (request.Type)
            {
                case VerifyIdentityType.Password:
                    if (string.IsNullOrWhiteSpace(request.Password) || !VerifyPassword(user, request.Password))
                    {
                        throw new InvalidOperationException("密码验证失败");
                    }
                    break;
                case VerifyIdentityType.Phone:
                    if (string.IsNullOrWhiteSpace(user.Phone))
                    {
                        throw new InvalidOperationException("当前账号未绑定手机");
                    }

                    if (!verificationCodeProvider.TryConsume(VerificationCodePurpose.VerifyIdentity, user.Phone, request.Code))
                    {
                        throw new InvalidOperationException("手机验证码错误");
                    }
                    break;
                case VerifyIdentityType.Email:
                    if (string.IsNullOrWhiteSpace(user.Email))
                    {
                        throw new InvalidOperationException("当前账号未绑定邮箱");
                    }

                    if (!verificationCodeProvider.TryConsume(VerificationCodePurpose.VerifyIdentity, user.Email, request.Code))
                    {
                        throw new InvalidOperationException("邮箱验证码错误");
                    }
                    break;
                default:
                    throw new InvalidOperationException("验证方式无效");
            }

            var verifyToken = Guid.NewGuid().ToString("N");
            memoryCache.Set(GetVerifyTokenCacheKey(verifyToken), new VerifyIdentityTicket
            {
                UserId = userId,
                VerifiedAt = DateTime.UtcNow,
            }, VerifyTokenTtl);

            return Task.FromResult(new VerifyIdentityResponse
            {
                VerifyToken = verifyToken,
                ExpireAt = DateTime.UtcNow.Add(VerifyTokenTtl),
            });
        }

        public async Task ChangePasswordAsync(string userId, ChangePasswordRequest request)
        {
            var user = ConsumeVerifyTicket(userId, request.VerifyToken);

            if (string.IsNullOrWhiteSpace(request.NewPassword))
            {
                throw new InvalidOperationException("新密码不能为空");
            }

            if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("两次输入的新密码不一致");
            }

            ValidatePasswordStrength(request.NewPassword, "新密码");

            user.Password = HKH.Common.Security.BCrypt.HashPassword(request.NewPassword);
            await dbContext.UpdateUser(user);
        }

        public async Task ChangePhoneAsync(string userId, ChangePhoneRequest request)
        {
            var user = ConsumeVerifyTicket(userId, request.VerifyToken);

            var phone = NormalizeAndValidateTarget(PinCodeTargetType.Phone, request.Phone);
            EnsureTargetAvailable(PinCodeTargetType.Phone, phone, user.Id);
            if (!verificationCodeProvider.TryConsume(VerificationCodePurpose.Bind, phone, request.Code))
            {
                throw new InvalidOperationException("验证码错误");
            }

            user.Phone = phone;
            await dbContext.UpdateUser(user);
        }

        public async Task ChangeEmailAsync(string userId, ChangeEmailRequest request)
        {
            var user = ConsumeVerifyTicket(userId, request.VerifyToken);

            var email = NormalizeAndValidateTarget(PinCodeTargetType.Email, request.Email);
            EnsureTargetAvailable(PinCodeTargetType.Email, email, user.Id);
            if (!verificationCodeProvider.TryConsume(VerificationCodePurpose.Bind, email, request.Code))
            {
                throw new InvalidOperationException("验证码错误");
            }

            user.Email = email;
            await dbContext.UpdateUser(user);
        }

        public async Task UnbindPhoneAsync(string userId, UnbindPhoneRequest request)
        {
            var user = ConsumeVerifyTicket(userId, request.VerifyToken);

            if (string.IsNullOrWhiteSpace(user.Phone))
            {
                throw new InvalidOperationException("当前未绑定手机");
            }

            if (string.IsNullOrWhiteSpace(user.Email))
            {
                throw new InvalidOperationException("当前仅剩手机，不能解绑");
            }

            user.Phone = string.Empty;
            await dbContext.UpdateUser(user);
        }

        public async Task UnbindEmailAsync(string userId, UnbindEmailRequest request)
        {
            var user = ConsumeVerifyTicket(userId, request.VerifyToken);

            if (string.IsNullOrWhiteSpace(user.Email))
            {
                throw new InvalidOperationException("当前未绑定邮箱");
            }

            if (string.IsNullOrWhiteSpace(user.Phone))
            {
                throw new InvalidOperationException("当前仅剩邮箱，不能解绑");
            }

            user.Email = string.Empty;
            await dbContext.UpdateUser(user);
        }

        private User GetCurrentUser(string userId)
        {
            return dbContext.Users.FirstOrDefault(x => x.Id == userId) ?? throw new InvalidOperationException("当前用户不存在");
        }

        private User ConsumeVerifyTicket(string userId, string verifyToken)
        {
            if (string.IsNullOrWhiteSpace(verifyToken))
            {
                throw new InvalidOperationException("缺少身份验证令牌");
            }

            if (!memoryCache.TryGetValue<VerifyIdentityTicket>(GetVerifyTokenCacheKey(verifyToken), out var ticket) || ticket == null)
            {
                throw new InvalidOperationException("身份验证已失效，请重新验证");
            }

            if (!string.Equals(ticket.UserId, userId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("身份验证令牌无效");
            }

            memoryCache.Remove(GetVerifyTokenCacheKey(verifyToken));
            return GetCurrentUser(userId);
        }

        private void EnsureTargetAvailable(string type, string target, string currentUserId)
        {
            User? duplicated = type == PinCodeTargetType.Phone
                ? dbContext.Users.FirstOrDefault(x => !x.Disabled && x.Phone == target)
                : dbContext.Users.FirstOrDefault(x => !x.Disabled && x.Email == target);
            if (duplicated != null && duplicated.Id != currentUserId)
            {
                throw new InvalidOperationException(type == PinCodeTargetType.Phone ? "手机号已存在" : "邮箱已存在");
            }
        }

        private static bool VerifyPassword(User user, string password)
        {
            return !string.IsNullOrWhiteSpace(user.Password) && HKH.Common.Security.BCrypt.Verify(password, user.Password);
        }

        private static void ValidatePasswordStrength(string password, string fieldName)
        {
            if (password.Length < 8 || password.Length > 30)
            {
                throw new InvalidOperationException($"{fieldName}长度需为8-30位");
            }

            var categories = 0;
            if (UppercaseRegex.IsMatch(password)) categories++;
            if (LowercaseRegex.IsMatch(password)) categories++;
            if (DigitRegex.IsMatch(password)) categories++;
            if (SpecialCharRegex.IsMatch(password)) categories++;

            if (categories < 3)
            {
                throw new InvalidOperationException($"{fieldName}需包含大写字母、小写字母、数字、特殊字符中的至少三种");
            }
        }

        private static string GetEmailName(string email)
        {
            var atIndex = email.IndexOf('@');
            return atIndex > 0 ? email[..atIndex] : email;
        }

        private static string NormalizeAndValidateTarget(string type, string? target)
        {
            if (string.IsNullOrWhiteSpace(target))
            {
                throw new InvalidOperationException("手机号或邮箱不能为空");
            }

            var normalizedTarget = target.Trim();
            if (type == PinCodeTargetType.Phone)
            {
                if (!PhoneRegex.IsMatch(normalizedTarget))
                {
                    throw new InvalidOperationException("手机号格式不正确");
                }

                return normalizedTarget;
            }

            normalizedTarget = normalizedTarget.ToLowerInvariant();
            if (!EmailRegex.IsMatch(normalizedTarget))
            {
                throw new InvalidOperationException("邮箱格式不正确");
            }

            return normalizedTarget;
        }

        private static bool IsUsage(string usage)
        {
            return usage == PinCodeUsage.Verify || usage == PinCodeUsage.Bind;
        }

        private static bool IsTargetType(string type)
        {
            return type == PinCodeTargetType.Phone || type == PinCodeTargetType.Email;
        }

        private static string GetVerifyTokenCacheKey(string token) => $"auth:verifyIdentity:{token}";
    }
}
