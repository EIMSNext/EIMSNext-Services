using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Entities;
using Microsoft.OData.ModelBuilder;

namespace EIMSNext.Service.Host.Edm
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
