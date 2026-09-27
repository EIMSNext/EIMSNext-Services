using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;

namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// OData 写入请求的公共基类。
    /// </summary>
    public abstract class RequestBase : IMongoEntity
    {
        /// <summary>
        /// 资源标识。创建请求不传，更新请求必须与 URL 中的标识一致。
        /// </summary>
        public string Id { get; set; } = "";
    }
}
