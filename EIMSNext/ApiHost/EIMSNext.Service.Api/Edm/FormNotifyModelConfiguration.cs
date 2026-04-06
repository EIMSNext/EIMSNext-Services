using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using Microsoft.OData.ModelBuilder;

namespace EIMSNext.Service.Api.Edm
{
    /// <summary>
    /// 
    /// </summary>
    public class FormNotifyModelConfiguration : CorpModelConfigurationBase<FormNotifyViewModel, FormNotifyRequest>
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entityType"></param>
        protected override void ConfigureCommon(EntityTypeConfiguration<FormNotifyViewModel> entityType)
        {
            base.ConfigureCommon(entityType);

            entityType.Ignore(x => x.DataDynamicFilter);
        }
    }
}
