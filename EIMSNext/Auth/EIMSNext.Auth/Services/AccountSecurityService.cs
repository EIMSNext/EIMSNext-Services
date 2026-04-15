using System.Text.RegularExpressions;
using EIMSNext.Auth.AccountSecurity;
using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace EIMSNext.Auth.Services
{
    public class AccountSecurityService(
        IUserService userService,
        IAuthDbContext dbContext,
        IMemoryCache memoryCache) : IAccountSecurityService
    {
        private const string MockPinCode = "123456";
        private static readonly TimeSpan VerifyTokenTtl = TimeSpan.FromMinutes(10);
        private static readonly Regex PhoneRegex = new("^1[3-9]\\d{9}$", RegexOptions.Compiled);
        private static readonly Regex EmailRegex = new(@"^\w[-\w.+]*@([A-Za-z0-9][-A-Za-z0-9]+\.)+[A-Za-z]{2,14}$", RegexOptions.Compiled);

        public Task SendPinCodeAsync(string userId, SendPinCodeRequest request)
        {
            var user = GetCurrentUser(userId);

            if (string.IsNullOrWhiteSpace(request.Target))
            {
                throw new InvalidOperationException("手机号或邮箱不能为空");
            }

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
                if (request.Type == PinCodeTargetType.Phone)
                {
                    if (string.IsNullOrWhiteSpace(user.Phone))
                    {
                        throw new InvalidOperationException("当前账号未绑定手机");
                    }

                    if (!string.Equals(user.Phone, request.Target, StringComparison.Ordinal))
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

                    if (!string.Equals(user.Email, request.Target, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("邮箱与当前账号绑定邮箱不一致");
                    }
                }
            }
            else
            {
                EnsureTargetAvailable(request.Type, request.Target, user.Id);
            }

            return Task.CompletedTask;
        }

        public Task<VerifyIdentityResponse> VerifyIdentityAsync(string userId, VerifyIdentityRequest request)
        {
            var user = GetCurrentUser(userId);

            switch (request.Type)
            {
                case VerifyIdentityType.Password:
                    if (string.IsNullOrWhiteSpace(request.Password) || !userService.VerifyPassword(user, request.Password))
                    {
                        throw new InvalidOperationException("密码验证失败");
                    }
                    break;
                case VerifyIdentityType.Phone:
                    if (string.IsNullOrWhiteSpace(user.Phone))
                    {
                        throw new InvalidOperationException("当前账号未绑定手机");
                    }

                    if (!string.Equals(request.Code, MockPinCode, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("手机验证码错误");
                    }
                    break;
                case VerifyIdentityType.Email:
                    if (string.IsNullOrWhiteSpace(user.Email))
                    {
                        throw new InvalidOperationException("当前账号未绑定邮箱");
                    }

                    if (!string.Equals(request.Code, MockPinCode, StringComparison.Ordinal))
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

            if (request.NewPassword.Length < 6)
            {
                throw new InvalidOperationException("新密码至少6位");
            }

            if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("两次输入的新密码不一致");
            }

            user.Password = HKH.Common.Security.BCrypt.HashPassword(request.NewPassword);
            await dbContext.UpdateUser(user);
        }

        public async Task ChangePhoneAsync(string userId, ChangePhoneRequest request)
        {
            var user = ConsumeVerifyTicket(userId, request.VerifyToken);

            if (string.IsNullOrWhiteSpace(request.Phone))
            {
                throw new InvalidOperationException("手机号不能为空");
            }

            if (!PhoneRegex.IsMatch(request.Phone))
            {
                throw new InvalidOperationException("手机号格式不正确");
            }

            if (!string.Equals(request.Code, MockPinCode, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("验证码错误");
            }

            EnsureTargetAvailable(PinCodeTargetType.Phone, request.Phone, user.Id);

            user.Phone = request.Phone;
            await dbContext.UpdateUser(user);
        }

        public async Task ChangeEmailAsync(string userId, ChangeEmailRequest request)
        {
            var user = ConsumeVerifyTicket(userId, request.VerifyToken);

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                throw new InvalidOperationException("邮箱不能为空");
            }

            if (!EmailRegex.IsMatch(request.Email))
            {
                throw new InvalidOperationException("邮箱格式不正确");
            }

            if (!string.Equals(request.Code, MockPinCode, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("验证码错误");
            }

            EnsureTargetAvailable(PinCodeTargetType.Email, request.Email, user.Id);

            user.Email = request.Email;
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
            return userService.FindById(userId) ?? throw new InvalidOperationException("当前用户不存在");
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
            User? duplicated = type == PinCodeTargetType.Phone ? userService.FindByPhone(target) : userService.FindByEmail(target);
            if (duplicated != null && duplicated.Id != currentUserId)
            {
                throw new InvalidOperationException(type == PinCodeTargetType.Phone ? "手机号已存在" : "邮箱已存在");
            }
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
