using EIMSNext.Core.Entities;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.Entities;

using Microsoft.OData.ModelBuilder;

namespace EIMSNext.Service.Host.Edm
{
    /// <summary>
    /// 
    /// </summary>
    public class FormDefModelConfiguration : CorpModelConfigurationBase<FormDef,FormDefRequest>
    {
        protected override void ConfigureCommon(EntityTypeConfiguration<FormDef> entityType)
        {
            base.ConfigureCommon(entityType);
            entityType.Ignore(x => x.PublicRelatedFormIds);
        }
    }
}

