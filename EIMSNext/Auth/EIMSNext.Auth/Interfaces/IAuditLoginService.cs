using EIMSNext.Auth.Entities;

namespace EIMSNext.Auth.Interfaces
{
    public interface IAuditLoginService
    {
        Task AddAuditLogin(AuditLogin entity);
    }
}
