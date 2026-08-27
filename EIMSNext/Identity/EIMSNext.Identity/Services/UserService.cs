using EIMSNext.Entities;
using EIMSNext.Identity.Interfaces;
using Microsoft.Extensions.Logging;

namespace EIMSNext.Identity.Services
{
    public class UserService : IUserService
    {
        private readonly IIdentityDbContext _dbContext;
        private readonly ILogger<UserService> _logger;

        public UserService(IIdentityDbContext dbContext, ILogger<UserService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger;
        }

        public User? Validate(string emailOrPhone, string password)
        {
            var user = FindByEmailOrPhone(emailOrPhone);
            if (user != null)
            {
                if (!string.IsNullOrWhiteSpace(user.Password) && HKH.Common.Security.BCrypt.Verify(password, user.Password))
                {
                    return user;
                }

                return null;
            }

            return null;
        }

        public User? FindById(string id)
        {
            return _dbContext.Users.FirstOrDefault(x => x.Id == id);
        }

        public User? FindByEmailOrPhone(string emailOrPhone)
        {
            var normalized = emailOrPhone?.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            return _dbContext.Users.FirstOrDefault(x =>
                !x.Disabled &&
                (string.Equals(x.Email, normalized, StringComparison.OrdinalIgnoreCase) || x.Phone == normalized));
        }

        public User? FindByEmail(string email)
        {
            var normalized = email?.Trim();
            return string.IsNullOrWhiteSpace(normalized)
                ? null
                : _dbContext.Users.FirstOrDefault(x =>
                    !x.Disabled && string.Equals(x.Email, normalized, StringComparison.OrdinalIgnoreCase));
        }

        public User? FindByPhone(string phone)
        {
            var normalized = phone?.Trim();
            return string.IsNullOrWhiteSpace(normalized)
                ? null
                : _dbContext.Users.FirstOrDefault(x => !x.Disabled && x.Phone == normalized);
        }

        public User? FindByEmpNo(string corpId, string empNo)
        {
            if (string.IsNullOrWhiteSpace(corpId) || string.IsNullOrWhiteSpace(empNo))
            {
                return null;
            }

            var employee = _dbContext.Employees.FirstOrDefault(x =>
                x.CorpId == corpId &&
                x.Code == empNo &&
                x.Status == 0 &&
                !string.IsNullOrWhiteSpace(x.UserId));

            return employee == null
                ? null
                : _dbContext.Users.FirstOrDefault(x => x.Id == employee.UserId && !x.Disabled);
        }

        public Client? FindEnabledClient(string clientId)
        {
            return _dbContext.Clients.FirstOrDefault(x => x.Id == clientId && x.Enabled);
        }

        public bool VerifyPassword(User user, string password)
        {
            return !string.IsNullOrWhiteSpace(user.Password) && HKH.Common.Security.BCrypt.Verify(password, user.Password);
        }
    }
}
