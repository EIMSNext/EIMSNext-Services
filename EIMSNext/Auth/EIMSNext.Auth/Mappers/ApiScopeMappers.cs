using AutoMapper;
using EIMSNext.Auth.Entity;
using Models = IdentityServer4.Models;

namespace EIMSNext.Auth.Mappers
{
    public static class ApiScopeMappers
    {
        static ApiScopeMappers()
        {
            Mapper = new MapperConfiguration(cfg => cfg.AddProfile<ApiScopeMapperProfile>())
                .CreateMapper();
        }

        internal static IMapper Mapper { get; }

        public static Models.ApiScope ToModel(this ApiScope scope)
        {
            return Mapper.Map<Models.ApiScope>(scope);
        }

        public static ApiScope ToEntity(this Models.ApiScope scope)
        {
            return Mapper.Map<ApiScope>(scope);
        }

    }
}
