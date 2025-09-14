using AutoMapper;
using EIMSNext.Auth.Entity;
using Models = IdentityServer4.Models;

namespace EIMSNext.Auth.Mappers
{
    public static class ApiResourceMappers
    {
        static ApiResourceMappers()
        {
            Mapper = new MapperConfiguration(cfg => cfg.AddProfile<ApiResourceMapperProfile>())
                .CreateMapper();
        }

        internal static IMapper Mapper { get; }

        public static Models.ApiResource ToModel(this ApiResource resource)
        {
            return Mapper.Map<Models.ApiResource>(resource);
        }

        public static ApiResource ToEntity(this Models.ApiResource resource)
        {
            return Mapper.Map<ApiResource>(resource);
        }
    }
}