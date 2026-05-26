using EIMSNext.Auth.AccountSecurity;
using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Interfaces;
using EIMSNext.Auth.Services;
using Microsoft.Extensions.Caching.Memory;

namespace EIMSNext.Auth.Tests
{
    [TestClass]
    public class AccountSecurityServiceTests
    {
        [TestMethod]
        public async Task SendRegCodeAsync_Throws_WhenPhoneAlreadyExists()
        {
            using var dbContext = new FakeAuthDbContext(
            [
                new User { Id = "user-1", Phone = "13800138000", Email = "exists@test.com", Password = "hashed" }
            ]);
            var service = CreateService(dbContext);

            var ex = await AssertThrowsAsync<InvalidOperationException>(() => service.SendRegCodeAsync(new SendRegCodeRequest
            {
                Type = PinCodeTargetType.Phone,
                Target = "13800138000"
            }));

            Assert.AreEqual("手机号已存在", ex.Message);
        }

        [TestMethod]
        public async Task RegisterAsync_CreatesPhoneUser_WithPublicPlatformAndPhoneName()
        {
            using var dbContext = new FakeAuthDbContext();
            var service = CreateService(dbContext);

            await service.RegisterAsync(new RegisterRequest
            {
                Type = PinCodeTargetType.Phone,
                Phone = "13800138000",
                Code = "123456",
                Password = "Strong123!"
            });

            var created = dbContext.Users.Single();
            Assert.AreEqual("13800138000", created.Phone);
            Assert.AreEqual("13800138000", created.Name);
            Assert.AreEqual(EIMSNext.Core.Entities.PlatformType.Public, created.Platform);
            Assert.IsTrue(HKH.Common.Security.BCrypt.Verify("Strong123!", created.Password));
        }

        [TestMethod]
        public async Task RegisterAsync_CreatesEmailUser_WithEmailPrefixName()
        {
            using var dbContext = new FakeAuthDbContext();
            var service = CreateService(dbContext);

            await service.RegisterAsync(new RegisterRequest
            {
                Type = PinCodeTargetType.Email,
                Email = "tester@example.com",
                Code = "123456",
                Password = "Strong123!"
            });

            var created = dbContext.Users.Single();
            Assert.AreEqual("tester@example.com", created.Email);
            Assert.AreEqual("tester", created.Name);
        }

        [TestMethod]
        public async Task RegisterAsync_Throws_WhenPasswordIsWeak()
        {
            using var dbContext = new FakeAuthDbContext();
            var service = CreateService(dbContext);

            var ex = await AssertThrowsAsync<InvalidOperationException>(() => service.RegisterAsync(new RegisterRequest
            {
                Type = PinCodeTargetType.Email,
                Email = "tester@example.com",
                Code = "123456",
                Password = "abcdefghi"
            }));

            Assert.AreEqual("密码需包含大写字母、小写字母、数字、特殊字符中的至少三种", ex.Message);
        }

        [TestMethod]
        public async Task ChangePasswordAsync_Throws_WhenConfirmPasswordMismatch()
        {
            using var dbContext = new FakeAuthDbContext(
            [
                new User { Id = "user-1", Phone = "13800138000", Password = HKH.Common.Security.BCrypt.HashPassword("Old123!!") }
            ]);
            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            memoryCache.Set("auth:verifyIdentity:token-1", new VerifyIdentityTicketProxy { UserId = "user-1" }.ToTicket(), TimeSpan.FromMinutes(10));
            var service = new AccountSecurityService(dbContext, memoryCache);

            var ex = await AssertThrowsAsync<InvalidOperationException>(() => service.ChangePasswordAsync("user-1", new ChangePasswordRequest
            {
                VerifyToken = "token-1",
                NewPassword = "Strong123!",
                ConfirmPassword = "Strong123@"
            }));

            Assert.AreEqual("两次输入的新密码不一致", ex.Message);
        }

        [TestMethod]
        public async Task ChangePasswordAsync_Throws_WhenPasswordIsWeak()
        {
            using var dbContext = new FakeAuthDbContext(
            [
                new User { Id = "user-1", Phone = "13800138000", Password = HKH.Common.Security.BCrypt.HashPassword("Old123!!") }
            ]);
            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            memoryCache.Set("auth:verifyIdentity:token-1", new VerifyIdentityTicketProxy { UserId = "user-1" }.ToTicket(), TimeSpan.FromMinutes(10));
            var service = new AccountSecurityService(dbContext, memoryCache);

            var ex = await AssertThrowsAsync<InvalidOperationException>(() => service.ChangePasswordAsync("user-1", new ChangePasswordRequest
            {
                VerifyToken = "token-1",
                NewPassword = "abcdefghi",
                ConfirmPassword = "abcdefghi"
            }));

            Assert.AreEqual("新密码需包含大写字母、小写字母、数字、特殊字符中的至少三种", ex.Message);
        }

        private static AccountSecurityService CreateService(FakeAuthDbContext dbContext)
        {
            return new AccountSecurityService(dbContext, new MemoryCache(new MemoryCacheOptions()));
        }

        private static async Task<TException> AssertThrowsAsync<TException>(Func<Task> action) where TException : Exception
        {
            try
            {
                await action();
            }
            catch (TException ex)
            {
                return ex;
            }

            Assert.Fail($"Expected exception of type {typeof(TException).Name}");
            throw new InvalidOperationException("Unreachable");
        }

        private sealed class FakeAuthDbContext : IAuthDbContext
        {
            private readonly List<User> _users;
            private readonly List<Client> _clients = [];
            private readonly List<AuditLogin> _auditLogins = [];

            public FakeAuthDbContext(IEnumerable<User>? users = null)
            {
                _users = users?.ToList() ?? [];
            }

            public IQueryable<Client> Clients => _clients.AsQueryable();
            public IQueryable<User> Users => _users.AsQueryable();

            public Task AddClient(Client entity)
            {
                _clients.Add(entity);
                return Task.CompletedTask;
            }

            public Task AddUser(User entity)
            {
                if (string.IsNullOrWhiteSpace(entity.Id))
                {
                    entity.Id = Guid.NewGuid().ToString("N");
                }

                _users.Add(entity);
                return Task.CompletedTask;
            }

            public Task UpdateUser(User entity)
            {
                var index = _users.FindIndex(x => x.Id == entity.Id);
                if (index >= 0)
                {
                    _users[index] = entity;
                }

                return Task.CompletedTask;
            }

            public Task AddAuditLogin(AuditLogin entity)
            {
                _auditLogins.Add(entity);
                return Task.CompletedTask;
            }

            public void Dispose()
            {
            }
        }

        private sealed class VerifyIdentityTicketProxy
        {
            public string UserId { get; set; } = string.Empty;

            public object ToTicket()
            {
                var ticketType = typeof(AccountSecurityService).Assembly.GetType("EIMSNext.Auth.AccountSecurity.VerifyIdentityTicket")!;
                var ticket = Activator.CreateInstance(ticketType)!;
                ticketType.GetProperty("UserId")!.SetValue(ticket, UserId);
                ticketType.GetProperty("VerifiedAt")!.SetValue(ticket, DateTime.UtcNow);
                return ticket;
            }
        }
    }
}
