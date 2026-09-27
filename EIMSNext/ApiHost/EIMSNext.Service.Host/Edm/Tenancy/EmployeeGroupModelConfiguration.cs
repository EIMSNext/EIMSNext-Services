using System.Reflection.Emit;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OData.ModelBuilder;

namespace EIMSNext.Service.Host.Edm
{
    /// <summary>
    /// 
    /// </summary>
    public class EmployeeGroupModelConfiguration : CorpModelConfigurationBase<EmployeeGroupViewModel, EmployeeGroupRequest>
    {
        protected override void ConfigureCommon(EntityTypeConfiguration<EmployeeGroupViewModel> entityType)
        {
            base.ConfigureCommon(entityType);
        }
    }
}
