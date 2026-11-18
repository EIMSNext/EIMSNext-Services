using EIMSNext.Core.Entities;
using EIMSNext.Entities;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Host.Edm;
using Microsoft.OData.ModelBuilder;

namespace EIMSNext.API.EdmModelConfiguration
{
    /// <summary>
    /// 
    /// </summary>
    public class WfTaskLogModelConfiguration : LogModelConfigurationBase<Wf_TaskLog>
    {
        protected override void ConfigureEntitySet(ODataModelBuilder builder)
        {
            builder.EntitySet<Wf_TaskLog>("WfTaskLog");
        }
    }
}

