using EIMSNext.Core.Service;
using EIMSNext.Entity;

using MongoDB.Driver;

namespace EIMSNext.Service.Interface
{
    public interface IFormDataService : IService<FormData>
    {
        Task SubmitAsync(IEnumerable<FormData> entities, IClientSessionHandle? session, CascadeMode cascade, string? eventIds);

        //void EvalFormulas(FormDef formDef, IEnumerable<FormData> formDatas);
    }
}
