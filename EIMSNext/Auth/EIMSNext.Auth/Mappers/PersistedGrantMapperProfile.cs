using AutoMapper;
using Models = IdentityServer4.Models;

namespace EIMSNext.Auth.Mappers
{
    /// <summary>
    /// AutoMapper Config for PersistedGrant
    /// Between Model and Entity
    /// <seealso cref="https://github.com/AutoMapper/AutoMapper/wiki/Configuration">
    /// </seealso>
    /// </summary>
    public class PersistedGrantMapperProfile : Profile
    {
        /// <summary>
        /// <see cref="PersistedGrantMapperProfile">
        /// </see>
        /// </summary>
        public PersistedGrantMapperProfile()
        {
            // entity to model
            CreateMap<Entity.PersistedGrant, Models.PersistedGrant>(MemberList.Destination)
                .ForMember(x => x.Key, opt => opt.MapFrom(src => src.Id))
                .ForMember(x => x.SubjectId, opt => opt.MapFrom(src => src.UserId));

            // model to entity
            CreateMap<Models.PersistedGrant, Entity.PersistedGrant>(MemberList.Source)
                .ForMember(x => x.Id, opt => opt.MapFrom(src => src.Key))
                .ForMember(x => x.UserId, opt => opt.MapFrom(src => src.SubjectId));
        }
    }
}
