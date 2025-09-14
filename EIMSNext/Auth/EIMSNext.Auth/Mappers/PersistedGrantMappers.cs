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

        public static PersistedGrant ToModel(this Entity.PersistedGrant token)
        {
            return Mapper.Map<PersistedGrant>(token);
        }

        public static Entity.PersistedGrant ToEntity(this PersistedGrant token)
        {
            return Mapper.Map<Entity.PersistedGrant>(token);
        }

        public static void UpdateEntity(this PersistedGrant token, Entity.PersistedGrant target)
        {
            Mapper.Map(token, target);
        }
    }
}