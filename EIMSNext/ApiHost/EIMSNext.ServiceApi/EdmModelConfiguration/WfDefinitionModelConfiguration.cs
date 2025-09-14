using EIMSNext.ApiService.RequestModel;
using EIMSNext.ApiService.ViewModel;

using Microsoft.OData.ModelBuilder;

namespace EIMSNext.ServiceApi.EdmModelConfiguration
{
    /// <summary>
    /// 
    /// </summary>
    public class WfDefinitionModelConfiguration : CorpModelConfigurationBase<WfDefinitionViewModel, WfDefinitionRequest>
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entityType"></param>
        protected override void ConfigureCommon(EntityTypeConfiguration<WfDefinitionViewModel> entityType)
        {
            base.ConfigureCommon(entityType);

            entityType.Ignore(x => x.Metadata);
            entityType.Ignore(x => x.EventSetting);
        }
    }
}
