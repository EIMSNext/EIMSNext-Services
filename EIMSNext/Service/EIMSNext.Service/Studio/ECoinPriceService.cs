using EIMSNext.Common;
using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;
using EIMSNext.Entities;
using HKH.Mef2.Integration;
using MongoDB.Driver;

namespace EIMSNext.Service
{
    public class ECoinPriceService(IResolver resolver) : MongoEntityServiceBase<ECoinPrice>(resolver), IECoinPriceService
    {
        public async Task<IReadOnlyList<ECoinPrice>> BatchUpsertAsync(IReadOnlyList<ECoinPrice> items)
        {
            return await ExecuteWithTransactionRetryAsync(async session =>
            {
            var result = new List<ECoinPrice>(items.Count);
            foreach (var item in items)
            {
                var current = Repository.Find(x =>
                    x.TargetType == item.TargetType && x.FeatureId == item.FeatureId, session).FirstOrDefault();
                if (current == null)
                {
                    item.Id = Repository.NewId();
                    await Repository.InsertAsync(item, session);
                    result.Add(item);
                }
                else
                {
                    current.FeatureDesc = item.FeatureDesc;
                    current.Price = item.Price;
                    current.ChargeType = item.ChargeType;
                    current.PluginId = item.PluginId;
                    await Repository.ReplaceAsync(current, session);
                    result.Add(current);
                }
            }
            return (IReadOnlyList<ECoinPrice>)result;
            }).ConfigureAwait(false);
        }
    }
}
