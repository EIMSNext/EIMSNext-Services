using EIMSNext.Common;
using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;
using EIMSNext.Entities;
using HKH.Mef2.Integration;

namespace EIMSNext.Service
{
    public class ECoinPriceService(IResolver resolver) : MongoEntityServiceBase<ECoinPrice>(resolver), IECoinPriceService
    {
        public async Task<IReadOnlyList<ECoinPrice>> BatchUpsertAsync(IReadOnlyList<ECoinPrice> items)
        {
            var result = new List<ECoinPrice>(items.Count);
            using var scope = NewTransactionScope();
            foreach (var item in items)
            {
                var existing = Repository.Queryable.FirstOrDefault(x =>
                    x.TargetType == item.TargetType && x.FeatureId == item.FeatureId);
                if (existing == null)
                {
                    item.Id = Repository.NewId();
                    await Repository.InsertAsync(item);
                    result.Add(item);
                }
                else
                {
                    existing.FeatureDesc = item.FeatureDesc;
                    existing.Price = item.Price;
                    existing.ChargeType = item.ChargeType;
                    existing.PluginId = item.PluginId;
                    await Repository.ReplaceAsync(existing);
                    result.Add(existing);
                }
            }
            scope.CommitTransaction();
            return result;
        }
    }
}
