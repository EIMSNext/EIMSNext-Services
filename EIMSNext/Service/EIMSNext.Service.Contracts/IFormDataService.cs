using EIMSNext.Core.Services;
using EIMSNext.Service.Entities;

using MongoDB.Driver;

namespace EIMSNext.Service.Contracts
{
    public interface IFormDataService : IService<FormData>
    {
        Task SubmitAsync(IEnumerable<FormData> entities, IClientSessionHandle? session, CascadeMode cascade, string? eventIds);

        //void EvalFormulas(FormDef formDef, IEnumerable<FormData> formDatas);
    }
}
