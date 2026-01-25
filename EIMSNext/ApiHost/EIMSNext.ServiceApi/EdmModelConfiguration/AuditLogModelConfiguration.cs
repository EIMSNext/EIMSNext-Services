using EIMSNext.ApiService.ViewModel;
using EIMSNext.Entity;
using Microsoft.OData.ModelBuilder;

namespace EIMSNext.ServiceApi.EdmModelConfiguration
{
    /// <summary>
    /// 
    /// </summary>
    public class AuditLogModelConfiguration : CorpModelConfigurationBase<AuditLogViewModel>
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entityType"></param>
        protected override void ConfigureCommon(EntityTypeConfiguration<AuditLogViewModel> entityType)
        {
            base.ConfigureCommon(entityType);

            entityType.Ignore(x => x.OldData);
            entityType.Ignore(x => x.NewData);
            entityType.Ignore(x => x.DataFilter);
            entityType.Ignore(x => x.UpdateExp);
        }
    }
}
