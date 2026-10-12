using EIMSNext.Core.Entities;
using EIMSNext.Entities;
using EIMSNext.ApiService.ViewModels;
using Microsoft.OData.ModelBuilder;

namespace EIMSNext.Service.Host.Edm
{
    /// <summary>
    ///
    /// </summary>
    public class EfRunLogNodeModelConfiguration : CorpModelConfigurationBase<Ef_RunLogNode>
    {
        protected override void ConfigureEntitySet(ODataModelBuilder builder)
        {
            builder.EntitySet<Ef_RunLogNode>("EfRunLogNode");
        }
    }
}

