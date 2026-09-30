using EIMSNext.Core.Query;
using EIMSNext.Core.Services;
using EIMSNext.Entities;

namespace EIMSNext.Service.Contracts
{
    public interface IFormDataService : IService<FormData>
    {
        /// <summary>
        /// 按主键取值并忽略软删除过滤。
        /// </summary>
        /// <remarks>
        /// 删除事件流在业务侧软删除之后才触发，此时全局过滤已隐藏该行，只有忽略过滤才能取到。
        /// </remarks>
        FormData? GetIncludingDeleted(string id);

        Task RestoreAsync(IEnumerable<string> ids);
        Task PurgeAsync(IEnumerable<string> ids);

        Task SubmitAsync(IEnumerable<FormData> entities, CascadeMode cascade, string? eventIds);

        Task<FilterOptionResult> GetFieldOptionsAsync(FilterOptionQuery query);
    }
}
