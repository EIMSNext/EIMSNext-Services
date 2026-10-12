using EIMSNext.Core.Entities;
using EIMSNext.Entities;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using Microsoft.OData.ModelBuilder;

namespace EIMSNext.Service.Host.Edm
{
    /// <summary>
    /// 
    /// </summary>
    public class FormNotifyModelConfiguration : CorpModelConfigurationBase<FormNotify, FormNotifyRequest>
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entityType"></param>
        protected override void ConfigureCommon(EntityTypeConfiguration<FormNotify> entityType)
        {
            base.ConfigureCommon(entityType);

            entityType.Ignore(x => x.DataDynamicFilter);
        }
    }
}

