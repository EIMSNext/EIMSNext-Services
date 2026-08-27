using EIMSNext.Core.Services;
using EIMSNext.Entities;

namespace EIMSNext.Service.Contracts
{
    public interface IECoinPriceService : IService<ECoinPrice>
    {
        Task<IReadOnlyList<ECoinPrice>> BatchUpsertAsync(IReadOnlyList<ECoinPrice> items);
    }
}
