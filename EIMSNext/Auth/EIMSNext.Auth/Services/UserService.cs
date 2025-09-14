using EIMSNext.Auth.Entity;
using EIMSNext.Auth.Interfaces;
using Microsoft.Extensions.Logging;

namespace EIMSNext.Auth.Services
{
    public class UserService : IUserService
    {
        private readonly IAuthDbContext _context;
        private readonly ILogger<UserService> _logger;

        public UserService(IAuthDbContext context, ILogger<UserService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
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
            return _context.Users.FirstOrDefault(x => x.Id == id);
        }

        public User? FindByEmailOrPhone(string emailOrPhone)
        {
            return _context.Users.FirstOrDefault(x => !x.Disabled && (emailOrPhone.Equals(x.Email, StringComparison.OrdinalIgnoreCase) || emailOrPhone.Equals(x.Phone, StringComparison.OrdinalIgnoreCase)));
        }

        public User? FindByEmail(string email)
        {
            return _context.Users.FirstOrDefault(x => !x.Disabled && email.Equals(x.Email, StringComparison.OrdinalIgnoreCase));
        }

        public User? FindByPhone(string phone)
        {
            return _context.Users.FirstOrDefault(x => !x.Disabled && phone.Equals(x.Phone, StringComparison.OrdinalIgnoreCase));
        }

        public User? FindByEmpNo(string corpId, string empNo)
        {
            throw new NotImplementedException();
        }

        //public Task<User?> FindByExternalProvider(string provider, string userId)
        //{
        //    return _users.FirstOrDefault((TestUser x) => x.ProviderName == provider && x.ProviderSubjectId == userId);
        //}
    }
}
