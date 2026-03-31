namespace EIMSNext.ApiService
{
    public interface IClientApiService : IApiService<EIMSNext.Auth.Entities.Client, EIMSNext.Auth.Entities.Client>
    {
        Task AddApiKeyAsync(EIMSNext.Auth.Entities.Client entity);
        Task RefreshApiKeyAsync(EIMSNext.Auth.Entities.Client entity);
    }
}
