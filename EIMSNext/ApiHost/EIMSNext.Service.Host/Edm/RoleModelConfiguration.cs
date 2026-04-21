using System.Reflection.Emit;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OData.ModelBuilder;

namespace EIMSNext.Service.Host.Edm
{
    /// <summary>
    /// 
    /// </summary>
    public class RoleModelConfiguration : CorpModelConfigurationBase<RoleViewModel, RoleRequest>
    {
        protected override void ConfigureCommon(EntityTypeConfiguration<RoleViewModel> entityType)
        {
            base.ConfigureCommon(entityType);
        }
    }
}
