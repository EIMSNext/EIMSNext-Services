using HKH.Mef2.Integration;

using EIMSNext.ApiService.Interface;

using NanoidDotNet;
using EIMSNext.Service.Interface;

namespace EIMSNext.ApiService
{
    public class ClientApiService(IResolver resolver) : ApiServiceBase<Auth.Entity.Client, Auth.Entity.Client, IClientService >(resolver), IClientApiService
    {
        public Task AddApiKeyAsync(Auth.Entity.Client entity)
        {
            entity.ApiKey = GeneratApiKey();
            return AddAsync(entity);
        }

        public Task RefreshApiKeyAsync(Auth.Entity.Client entity)
        {
            entity.ApiKey = GeneratApiKey();
            return ReplaceAsync(entity);
        }

        private string GeneratApiKey()
        {
            var alphabet = "_+-0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ!@#$%^&*()~`.,?=";
            return Nanoid.Generate(alphabet, 36);
        }
    }
}
