using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services;
using EIMSNext.Entities;

using MongoDB.Driver;

namespace EIMSNext.Service.Contracts
{
    public interface IFormDataService : IService<FormData>
    {
        void Add(IEnumerable<FormData> entities, IClientSessionHandle? session);
        ReplaceOneResult Replace(FormData entity, IClientSessionHandle? session);
        object Delete(IEnumerable<string> ids, IClientSessionHandle? session);
        Task RestoreAsync(IEnumerable<string> ids);
        Task PurgeAsync(IEnumerable<string> ids);

        Task SubmitAsync(IEnumerable<FormData> entities, IClientSessionHandle? session, CascadeMode cascade, string? eventIds);

        Task<FilterOptionResult> GetFieldOptionsAsync(FilterOptionQuery query);
    }
}
