using EIMSNext.ApiService.RequestModel;
using EIMSNext.Core.Entity;

using Microsoft.OData.ModelBuilder;

namespace EIMSNext.ServiceApi.EdmModelConfiguration
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class EntityModelConfigurationBase<T> : ModelConfigurationBase<T> where T : EntityBase
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entityType"></param>
        protected override void ConfigureCommon(EntityTypeConfiguration<T> entityType)
        {
            entityType.Ignore(x => x.CreateBy);
            //entityType.Ignore(x => x.CreateTime);
            entityType.Ignore(x => x.UpdateBy);
            entityType.Ignore(x => x.UpdateTime);
            entityType.Ignore(x => x.DeleteFlag);
        }
    }


    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="R"></typeparam>
    public abstract class EntityModelConfigurationBase<T, R> : ModelConfigurationBase<T, R> where T : EntityBase where R : RequestBase
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entityType"></param>
        protected override void ConfigureCommon(EntityTypeConfiguration<T> entityType)
        {
            entityType.Ignore(x => x.CreateBy);
            //entityType.Ignore(x => x.CreateTime);
            entityType.Ignore(x => x.UpdateBy);
            entityType.Ignore(x => x.UpdateTime);
            entityType.Ignore(x => x.DeleteFlag);
        }
    }
}
