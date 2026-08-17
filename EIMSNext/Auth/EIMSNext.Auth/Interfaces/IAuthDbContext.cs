using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Models;

namespace EIMSNext.Auth.Interfaces
{
    public interface IAuthDbContext : IDisposable
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
        Task AddAuditLogin(AuditLogin entity);
        Task AddAuditLogins(IReadOnlyCollection<AuditLogin> entities, CancellationToken cancellationToken = default)
        {
            return Task.WhenAll(entities.Select(AddAuditLogin));
        }
    }
}
