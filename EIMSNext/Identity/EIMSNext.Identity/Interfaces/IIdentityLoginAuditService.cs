using EIMSNext.Entities;

namespace EIMSNext.Identity.Interfaces
{
    public interface IIdentityLoginAuditService
    {
        Task AddIdentityLoginAudit(IdentityLoginAudit entity);
    }
}
