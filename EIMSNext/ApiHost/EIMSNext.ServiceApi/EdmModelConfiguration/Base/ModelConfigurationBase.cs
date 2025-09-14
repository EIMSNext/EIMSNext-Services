using Asp.Versioning;
using Asp.Versioning.OData;

using EIMSNext.ApiService.RequestModel;
using EIMSNext.Core.Entity;

using Microsoft.OData.ModelBuilder;

namespace EIMSNext.ServiceApi.EdmModelConfiguration
{
    /// <summary>
    /// 
    /// </summary>
    public abstract class ModelConfigurationBase : IModelConfiguration
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="apiVersion"></param>
        /// <param name="routePrefix"></param>
        /// <exception cref="NotImplementedException"></exception>
        public abstract void Apply(ODataModelBuilder builder, ApiVersion apiVersion, string? routePrefix);
    }


    /// <summary>
    /// 注册ViewModel
    /// </summary>
    /// <typeparam name="T">ViewModel</typeparam>
    public abstract class ModelConfigurationBase<T> : ModelConfigurationBase where T : class
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="apiVersion"></param>
        /// <param name="routePrefix"></param>
        public override void Apply(ODataModelBuilder builder, ApiVersion apiVersion, string? routePrefix)
        {
            builder.EntitySet<T>(typeof(T).Name.Replace("ViewModel", ""));

            switch (apiVersion.MajorVersion)
            {
                case 1:
                    ConfigureV1(builder);
                    break;
                case 2:
                    ConfigureV2(builder);
                    break;
                default:
                    ConfigureBase(builder);
                    break;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        protected virtual EntityTypeConfiguration<T> ConfigureBase(ODataModelBuilder builder)
        {
            var entityType = builder.EntityType<T>();
            ConfigureCommon(entityType);
            return entityType;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entityType"></param>
        protected virtual void ConfigureCommon(EntityTypeConfiguration<T> entityType)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="builder"></param>
        protected virtual void ConfigureV1(ODataModelBuilder builder)
        {
            ConfigureBase(builder);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="builder"></param>
        protected virtual void ConfigureV2(ODataModelBuilder builder)
        {
            ConfigureBase(builder);
        }
    }


    /// <summary>
    /// 注册ViewModel + Requst
    /// </summary>
    /// <typeparam name="T">ViewModel</typeparam>
    /// <typeparam name="R">Request</typeparam>
    public abstract class ModelConfigurationBase<T, R> : ModelConfigurationBase<T> where T : class where R : class
    {
        protected override EntityTypeConfiguration<T> ConfigureBase(ODataModelBuilder builder)
        {
            builder.ComplexType<R>();
            return base.ConfigureBase(builder);
        }
    }
}
