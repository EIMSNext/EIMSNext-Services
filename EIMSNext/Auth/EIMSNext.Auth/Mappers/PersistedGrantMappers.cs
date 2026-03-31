using AutoMapper;
using IdentityServer4.Models;

namespace EIMSNext.Auth.Mappers
{
    public static class PersistedGrantMappers
    {
        static PersistedGrantMappers()
        {
            Mapper = new MapperConfiguration(cfg => cfg.AddProfile<PersistedGrantMapperProfile>())
                .CreateMapper();
        }

        internal static IMapper Mapper { get; }

        public static PersistedGrant ToModel(this Entities.PersistedGrant token)
        {
            return Mapper.Map<PersistedGrant>(token);
        }

        public static Entities.PersistedGrant ToEntity(this PersistedGrant token)
        {
            return Mapper.Map<Entities.PersistedGrant>(token);
        }

        public static void UpdateEntity(this PersistedGrant token, Entities.PersistedGrant target)
        {
            Mapper.Map(token, target);
        }
    }
}
