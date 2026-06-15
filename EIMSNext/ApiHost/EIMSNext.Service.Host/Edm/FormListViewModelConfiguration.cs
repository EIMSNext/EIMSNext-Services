using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Entities;
using Microsoft.OData.ModelBuilder;

namespace EIMSNext.Service.Host.Edm
{
    public class FormListViewModelConfiguration : CorpModelConfigurationBase<FormListViewViewModel, FormListViewRequest>
    {
        protected override void ConfigureCommon(EntityTypeConfiguration<FormListViewViewModel> entityType)
        {
            base.ConfigureCommon(entityType);

            entityType.CollectionProperty(x => x.AuthGroupIds);
        }
    }
}
