using EIMSNext.Core.Entities;
using Microsoft.OData.ModelBuilder;

namespace EIMSNext.Service.Host.Edm
{
    /// <summary>日志实体公共 EDM 配置：仅隐藏 CorpId，其余字段按实体声明暴露。</summary>
    public abstract class LogModelConfigurationBase<T> : ModelConfigurationBase<T> where T : LogEntityBase
    {
        protected override void ConfigureCommon(EntityTypeConfiguration<T> entityType)
        {
            entityType.Ignore(x => x.CorpId);
        }
    }
}
