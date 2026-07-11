using EIMSNext.Auth.Entities;

namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// OAuth Client 的 OData POST/PUT/PATCH 输入模型。
    ///
    /// 故意省略 <c>ClientSecrets</c>、<c>ApiKey</c>：
    /// <list type="bullet">
    /// <item><c>ClientSecrets</c> 由 OData 创建和 Regenerate 端点控制（<see cref="ClientRequestModelConfiguration"/> 同样 Ignore）。</item>
    /// <item><c>ApiKey</c> 由系统生成，OData 上为只读。</item>
    /// </list>
    /// </summary>
    public class ClientRequest : RequestBase
    {
        public string? Name { get; set; }
        public bool Enabled { get; set; } = true;
    }
}
