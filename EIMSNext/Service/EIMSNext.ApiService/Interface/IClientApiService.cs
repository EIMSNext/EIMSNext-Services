namespace EIMSNext.ApiService
{
    public interface IClientApiService : IApiService<Auth.Entity.Client, Auth.Entity.Client>
    {
        Task AddApiKeyAsync(Auth.Entity.Client entity);
        Task RefreshApiKeyAsync(Auth.Entity.Client entity);
    }
}
