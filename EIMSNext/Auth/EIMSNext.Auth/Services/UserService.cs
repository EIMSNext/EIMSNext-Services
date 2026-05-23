using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Interfaces;
using Microsoft.Extensions.Logging;

namespace EIMSNext.Auth.Services
{
    public class UserService : IUserService
    {
        private readonly IAuthDbContext _dbContext;
        private readonly ILogger<UserService> _logger;

        public UserService(IAuthDbContext dbContext, ILogger<UserService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger;
        }

        public User? Validate(string emailOrPhone, string password)
        {
            var user = FindByEmailOrPhone(emailOrPhone);
            if (user != null)
            {
                if (password == Constants.NoPassword || HKH.Common.Security.BCrypt.Verify(password, user.Password))
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
            return _dbContext.Users.FirstOrDefault(x => !x.Disabled && (x.Email == emailOrPhone || x.Phone == emailOrPhone));
        }

        public User? FindByEmail(string email)
        {
            return _dbContext.Users.FirstOrDefault(x => !x.Disabled && x.Email == email);
        }

        public User? FindByPhone(string phone)
        {
            return _dbContext.Users.FirstOrDefault(x => !x.Disabled && x.Phone == phone);
        }

        public User? FindByEmpNo(string corpId, string empNo)
        {
            throw new NotImplementedException();
        }

        public Client? FindEnabledClient(string clientId)
        {
            return _dbContext.Clients.FirstOrDefault(x => x.Id == clientId && x.Enabled);
        }

        public bool VerifyPassword(User user, string password)
        {
            return password == Constants.NoPassword || HKH.Common.Security.BCrypt.Verify(password, user.Password);
        }
    }
}
