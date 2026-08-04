using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Entities;

using Microsoft.OData.ModelBuilder;

namespace EIMSNext.Service.Host.Edm
{
    /// <summary>
    /// 
    /// </summary>
    public class FormDefModelConfiguration : CorpModelConfigurationBase<FormDefViewModel,FormDefRequest>
    {
        protected override void ConfigureCommon(EntityTypeConfiguration<FormDefViewModel> entityType)
        {
            base.ConfigureCommon(entityType);
            entityType.Ignore(x => x.PublicRelatedFormIds);
        }
    }
}
