using EIMSNext.Core.Entities;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Entities;
using Microsoft.OData.ModelBuilder;

namespace EIMSNext.Service.Host.Edm
{
    /// <summary>
    /// 
    /// </summary>
    public class AuditLogModelConfiguration : CorpModelConfigurationBase<AuditLog>
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entityType"></param>
        protected override void ConfigureCommon(EntityTypeConfiguration<AuditLog> entityType)
        {
            base.ConfigureCommon(entityType);

            entityType.Ignore(x => x.OldData);
            entityType.Ignore(x => x.NewData);
            entityType.Ignore(x => x.DataFilter);
            entityType.Ignore(x => x.UpdateExp);
        }
    }
}

