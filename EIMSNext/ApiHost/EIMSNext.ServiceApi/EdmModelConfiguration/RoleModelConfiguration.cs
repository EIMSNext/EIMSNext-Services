using System.Reflection.Emit;
using EIMSNext.ApiService.RequestModel;
using EIMSNext.ApiService.ViewModel;
using EIMSNext.Entity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OData.ModelBuilder;

namespace EIMSNext.ServiceApi.EdmModelConfiguration
{
    /// <summary>
    /// 
    /// </summary>
    public class RoleModelConfiguration : CorpModelConfigurationBase<RoleViewModel, RoleRequest>
    {
        protected override void ConfigureCommon(EntityTypeConfiguration<RoleViewModel> entityType)
        {
            base.ConfigureCommon(entityType);

            var addEmps = entityType.Collection.Action("AddEmps");
            addEmps.Parameter<string>("roleId");
            addEmps.Parameter<string[]>("empIds");
            addEmps.Returns<ActionResult>();
        }
    }
}
