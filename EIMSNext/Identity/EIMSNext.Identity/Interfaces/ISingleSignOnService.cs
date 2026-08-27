using EIMSNext.Entities;

namespace EIMSNext.Identity.Interfaces
{
    public interface ISingleSignOnService
    {
        User? Validate(string? corp_empno, string? secret);
    }
}
