using EIMSNext.Auth.Entity;
using EIMSNext.Auth.Interfaces;

namespace EIMSNext.Auth.Services
{
    public class SingleSignOnService : ISingleSignOnService
    {
        private readonly IUserService _userService;
        public SingleSignOnService(IUserService userService)
        {
            _userService = userService;
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
            return true;
        }
    }
}
