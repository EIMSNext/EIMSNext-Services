using EIMSNext.Core.Entities;
using EIMSNext.Entities;
using EIMSNext.ApiService.ViewModels;
using Microsoft.OData.ModelBuilder;

namespace EIMSNext.Service.Host.Edm
{
    public class WfExecLogModelConfiguration : ModelConfigurationBase<Wf_ExecLog>
    {
        protected override void ConfigureEntitySet(ODataModelBuilder builder)
        {
            builder.EntitySet<Wf_ExecLog>("WfExecLog");
        }
    }
}

