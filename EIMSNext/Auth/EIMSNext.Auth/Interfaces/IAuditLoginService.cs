using EIMSNext.Auth.Entity;

namespace EIMSNext.Auth.Interfaces
{
    public interface IAuditLoginService
    {
        Task AddAuditLogin(AuditLogin entity);
    }
}
