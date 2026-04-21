using EIMSNext.Auth.Entities;

namespace EIMSNext.Auth.Interfaces
{
    public interface IAuthDbContext : IDisposable
    {
        IQueryable<Client> Clients { get; }
        IQueryable<User> Users { get; }

        Task AddClient(Client entity);

        Task AddUser(User entity);
        Task UpdateUser(User entity);
        Task AddAuditLogin(AuditLogin entity);
    }
}
