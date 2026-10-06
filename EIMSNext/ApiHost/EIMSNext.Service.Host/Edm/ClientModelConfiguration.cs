using EIMSNext.Core.Entities;
using EIMSNext.Entities;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;

using Microsoft.OData.ModelBuilder;

namespace EIMSNext.Service.Host.Edm
{
    /// <summary>
    /// <see cref="Client"/> 的 OData 模型注册。
    ///
    /// 关键安全策略：
    /// <list type="bullet">
    /// <item><c>ClientSecret</c> 用 <c>Ignore()</c> 整体从 OData 实体中移除（响应/请求都不含）。</item>
    /// <item><c>ApiKey</c> 由 <c>ClientApiService</c> 的 read-modify-write 保护，
    /// 普通 PATCH 不会改写该字段。</item>
    /// </list>
    /// </summary>
    public class ClientModelConfiguration : CorpModelConfigurationBase<Client, ClientRequest>
    {
        /// <inheritdoc />
        protected override void ConfigureCommon(EntityTypeConfiguration<Client> entityType)
        {
            base.ConfigureCommon(entityType);

            // ClientSecret 完全从 OData 实体中移除（响应/请求都不含）
            entityType.Ignore(x => x.ClientSecret);
            entityType.Ignore(x => x.ApiKey);
            entityType.Ignore(x => x.RequireClientSecret);
            entityType.Ignore(x => x.AccessTokenLifetime);
            entityType.Ignore(x => x.IdentityTokenLifetime);
            entityType.Ignore(x => x.AllowedGrantTypes);
            entityType.Ignore(x => x.AllowedScopes);
        }
    }
}

