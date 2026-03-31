using EIMSNext.ApiService.RequestModels;
using EIMSNext.Core.Entities;
using Microsoft.OData.ModelBuilder;

namespace EIMSNext.Service.Api.Edm
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class CorpModelConfigurationBase<T> : EntityModelConfigurationBase<T> where T : CorpEntityBase
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entityType"></param>
        protected override void ConfigureCommon(EntityTypeConfiguration<T> entityType)
        {
            base.ConfigureCommon(entityType);

            entityType.Ignore(x => x.CorpId);
        }
    }



    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="R"></typeparam>
    public abstract class CorpModelConfigurationBase<T, R> : EntityModelConfigurationBase<T, R> where T : CorpEntityBase where R : RequestBase
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entityType"></param>
        protected override void ConfigureCommon(EntityTypeConfiguration<T> entityType)
        {
            base.ConfigureCommon(entityType);

            entityType.Ignore(x => x.CorpId);
        }
    }
}
