using EIMSNext.Core.Query;
using EIMSNext.Core.Services;
using EIMSNext.Entities;

namespace EIMSNext.Service.Contracts
{
    public interface IFormDataService : IService<FormData>
    {
        Task RestoreAsync(IEnumerable<string> ids);
        Task PurgeAsync(IEnumerable<string> ids);

        Task SubmitAsync(IEnumerable<FormData> entities, CascadeMode cascade, string? eventIds);

        Task<FilterOptionResult> GetFieldOptionsAsync(FilterOptionQuery query);
    }
}
