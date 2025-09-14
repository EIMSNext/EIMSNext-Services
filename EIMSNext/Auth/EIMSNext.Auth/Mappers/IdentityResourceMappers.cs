using AutoMapper;
using EIMSNext.Auth.Entity;
using Models = IdentityServer4.Models;

namespace EIMSNext.Auth.Mappers
{
    public static class IdentityResourceMappers
    {
        static IdentityResourceMappers()
        {
            Mapper = new MapperConfiguration(cfg => cfg.AddProfile<IdentityResourceMapperProfile>())
                .CreateMapper();
        }

        internal static IMapper Mapper { get; }

        public static Models.IdentityResource ToModel(this IdentityResource resource)
        {
            return  Mapper.Map<Models.IdentityResource>(resource);
        }

        public static IdentityResource ToEntity(this Models.IdentityResource resource)
        {
            return Mapper.Map<IdentityResource>(resource);
        }
    }
}