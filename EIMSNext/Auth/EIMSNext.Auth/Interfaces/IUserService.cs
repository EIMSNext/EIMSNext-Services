using EIMSNext.Auth.Entity;

namespace EIMSNext.Auth.Interfaces
{
    public interface IUserService
    {
        User? Validate(string emailOrPhone, string password);
        User? FindById(string id);
        User? FindByEmailOrPhone(string emailOrPhone);
        User? FindByEmail(string email);
        User? FindByPhone(string phone);
        User? FindByEmpNo(string corpId, string empNo);
    }
}
