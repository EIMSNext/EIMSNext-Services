using EIMSNext.Auth.Entities;

namespace EIMSNext.Auth.Interfaces
{
    public interface ISingleSignOnService
    {
        User? Validate(string? corp_empno, string? secret);
    }
}
