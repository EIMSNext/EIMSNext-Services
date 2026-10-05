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
    public class EmployeeModelConfiguration : CorpModelConfigurationBase<Employee,EmployeeRequest>
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entityType"></param>
        protected override void ConfigureCommon(EntityTypeConfiguration<Employee> entityType)
        {
            base.ConfigureCommon(entityType);

            entityType.Ignore(x => x.Invite);
            entityType.Ignore(x => x.IsDummy);
            entityType.Ignore(x => x.IsSystem);
            entityType.Ignore(x => x.IsAnonymous);
        }
    }
}

