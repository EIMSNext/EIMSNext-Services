using EIMSNext.ApiService.RequestModel;
using EIMSNext.ApiService.ViewModel;

using Microsoft.OData.ModelBuilder;

namespace EIMSNext.ServiceApi.EdmModelConfiguration
{
    /// <summary>
    /// 
    /// </summary>
    public class EmployeeModelConfiguration : CorpModelConfigurationBase<EmployeeViewModel,EmployeeRequest>
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entityType"></param>
        protected override void ConfigureCommon(EntityTypeConfiguration<EmployeeViewModel> entityType)
        {
            base.ConfigureCommon(entityType);

            entityType.Ignore(x => x.Invite);
            entityType.Ignore(x => x.IsDummy);
            entityType.Ignore(x => x.IsSystem);
            entityType.Ignore(x => x.IsAnonymous);
        }
    }
}
