using EIMSNext.Core.Entities;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.Entities;
using Microsoft.OData.ModelBuilder;

namespace EIMSNext.Service.Host.Edm
{
    /// <summary>
    /// 
    /// </summary>
    public class WfTaskModelConfiguration : CorpModelConfigurationBase<Wf_Task, WfTaskRequest>
    {
        protected override void ConfigureEntitySet(ODataModelBuilder builder)
        {
            builder.EntitySet<Wf_Task>("WfTask");
        }
    }
}

