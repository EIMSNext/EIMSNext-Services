using EIMSNext.Auth.Entity;

namespace EIMSNext.Auth.Interfaces
{
    public interface IAuthDbContext : IDisposable
    {
        IQueryable<Client> Clients { get; }
        IQueryable<IdentityResource> IdentityResources { get; }
        IQueryable<ApiResource> ApiResources { get; }
        IQueryable<ApiScope> ApiScopes { get; }
        IQueryable<User> Users { get; }
        IQueryable<PersistedGrant> PersistedGrants { get; }

        Task AddClient(Client entity);

        Task AddIdentityResource(IdentityResource entity);

        Task AddApiResource(ApiResource entity);

        Task AddApiScope(ApiScope entity);

        Task AddUser(User entity);
        Task AddAuditLogin(AuditLogin entity);

        // TODO: 考虑把Grant放到缓存里
        Task RemovePersistedGrant(Expression<Func<PersistedGrant, bool>> filter);

        Task RemoveExpiredPersistedGrant();

        Task InsertOrUpdatePersistedGrant(Expression<Func<PersistedGrant, bool>> filter, PersistedGrant entity);
    }
}