using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace EIMSNext.Auth.Services
{
    public class SingleSignOnService : ISingleSignOnService
    {
        private readonly IUserService _userService;
        private readonly IAuthDbContext _dbContext;

        public SingleSignOnService(
            IUserService userService,
            IAuthDbContext dbContext)
        {
            _userService = userService;
            _dbContext = dbContext;
        }

        public User? Validate(string? corp_empno, string? secret)
        {
            var corp_empnos = corp_empno?.Split('_', StringSplitOptions.RemoveEmptyEntries);

            if (corp_empnos == null || corp_empnos.Length < 2 || !VerifySecret(corp_empnos[0], secret))
            {
                return null;
            }

            return _userService.FindByEmpNo(corp_empnos[0], corp_empnos[1]);
        }
        private bool VerifySecret(string corpId, string? secret)
        {
            var configuredSecret = _dbContext.CorporateSettings
                .Where(x => x.CorpId == corpId &&
                            x.Name == CorporateSettingNames.SsoSecret &&
                            !x.DeleteFlag)
                .Select(x => x.Value)
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(configuredSecret) || string.IsNullOrWhiteSpace(secret))
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(configuredSecret),
                Encoding.UTF8.GetBytes(secret));
        }
    }
}
