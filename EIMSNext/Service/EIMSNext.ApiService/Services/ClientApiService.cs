using EIMSNext.Service.Contracts;
using HKH.Mef2.Integration;
using NanoidDotNet;

namespace EIMSNext.ApiService
{
    public class ClientApiService(IResolver resolver) : ApiServiceBase<EIMSNext.Auth.Entities.Client, EIMSNext.Auth.Entities.Client, IClientService>(resolver), IClientApiService
    {
        public Task AddApiKeyAsync(EIMSNext.Auth.Entities.Client entity)
        {
            entity.ApiKey = GeneratApiKey();
            return AddAsync(entity);
        }

        public Task RefreshApiKeyAsync(EIMSNext.Auth.Entities.Client entity)
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
