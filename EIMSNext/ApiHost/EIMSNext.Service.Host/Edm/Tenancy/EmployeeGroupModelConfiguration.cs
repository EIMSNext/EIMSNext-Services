using EIMSNext.Core.Entities;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.Entities;
using Microsoft.OData.ModelBuilder;

namespace EIMSNext.Service.Host.Edm
{
    /// <summary>
    /// 
    /// </summary>
    public class EmployeeGroupModelConfiguration : CorpModelConfigurationBase<EmployeeGroup, EmployeeGroupRequest>
    {
        protected override void ConfigureCommon(EntityTypeConfiguration<EmployeeGroup> entityType)
        {
            base.ConfigureCommon(entityType);
            entityType.HasOptional(x => x.EmployeeGroupCategory);
        }
    }
}

