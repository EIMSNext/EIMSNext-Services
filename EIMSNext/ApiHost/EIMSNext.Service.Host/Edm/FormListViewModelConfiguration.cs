using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Entities;
using Microsoft.OData.ModelBuilder;

namespace EIMSNext.Service.Host.Edm
{
    public class FormListViewModelConfiguration : CorpModelConfigurationBase<FormListViewViewModel, FormListViewRequest>
    {
        protected override void ConfigureCommon(EntityTypeConfiguration<FormListViewViewModel> entityType)
        {
            base.ConfigureCommon(entityType);

            entityType.CollectionProperty(x => x.PermissionGroupIds);
        }
    }
}
