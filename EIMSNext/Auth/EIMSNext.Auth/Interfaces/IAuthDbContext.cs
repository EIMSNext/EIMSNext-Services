using EIMSNext.Auth.Entities;

namespace EIMSNext.Auth.Interfaces
{
    public interface IAuthDbContext : IDisposable
    {
        IQueryable<Client> Clients { get; }
        IQueryable<User> Users { get; }
        IQueryable<IntegrationLoginSetting> IntegrationLoginSettings { get; }
        IQueryable<UserIntegrationBinding> UserIntegrationBindings { get; }

        Task AddClient(Client entity);

        Task AddUser(User entity);
        Task UpdateUser(User entity);
        Task AddIntegrationLoginSetting(IntegrationLoginSetting entity);
        Task UpdateIntegrationLoginSetting(IntegrationLoginSetting entity);
        Task AddUserIntegrationBinding(UserIntegrationBinding entity);
        Task UpdateUserIntegrationBinding(UserIntegrationBinding entity);
        Task AddAuditLogin(AuditLogin entity);
    }
}
