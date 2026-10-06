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
    public class WfDefinitionModelConfiguration : CorpModelConfigurationBase<Wf_Definition, WfDefinitionRequest>
    {
        protected override void ConfigureEntitySet(ODataModelBuilder builder)
        {
            builder.EntitySet<Wf_Definition>("WfDefinition");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entityType"></param>
        protected override void ConfigureCommon(EntityTypeConfiguration<Wf_Definition> entityType)
        {
            base.ConfigureCommon(entityType);

            entityType.Ignore(x => x.Metadata);
            entityType.Ignore(x => x.EventSetting);
        }
    }
}

