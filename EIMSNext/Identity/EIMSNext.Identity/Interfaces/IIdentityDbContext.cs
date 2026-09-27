using EIMSNext.Entities;
using EIMSNext.Identity.Models;

namespace EIMSNext.Identity.Interfaces
{
    public interface IIdentityDbContext : IDisposable
    {
        IQueryable<Client> Clients { get; }
        IQueryable<User> Users { get; }
        IQueryable<EmployeeLookup> Employees { get; }
        IQueryable<PublicAccessSetting> PublicSettings { get; }
        IQueryable<CorporateSettingReadModel> CorporateSettings { get; }

        Task AddClient(Client entity);
        Task UpdateClient(Client entity);

        Task AddUser(User entity);
        Task UpdateUser(User entity);
        Task AddIdentityLoginAudit(IdentityLoginAudit entity);
        Task AddIdentityLoginAudits(IReadOnlyCollection<IdentityLoginAudit> entities, CancellationToken cancellationToken = default)
        {
            return Task.WhenAll(entities.Select(AddIdentityLoginAudit));
        }
    }
}
