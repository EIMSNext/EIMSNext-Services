using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Service.Contracts;
using EIMSNext.Entities;
using HKH.Mef2.Integration;

namespace EIMSNext.ApiService
{
    public class ECoinPriceApiService(IResolver resolver)
        : ApiServiceBase<ECoinPrice, ECoinPriceViewModel, IECoinPriceService>(resolver)
    {
        public async Task<IReadOnlyList<ECoinPrice>> BatchUpsertAsync(IEnumerable<ECoinPriceBatchItemRequest>? requests)
        {
            var normalized = NormalizeBatch(requests);
            return await CoreService.BatchUpsertAsync(normalized);
        }

        internal static List<ECoinPrice> NormalizeBatch(IEnumerable<ECoinPriceBatchItemRequest>? requests)
        {
            var normalized = (requests ?? []).Select(Normalize).ToList();
            if (normalized.Count == 0)
            {
                throw new BadRequestException("价格数据不能为空");
            }

            var duplicate = normalized
                .GroupBy(x => $"{x.TargetType}:{x.FeatureId}", StringComparer.Ordinal)
                .FirstOrDefault(x => x.Count() > 1);
            if (duplicate != null)
            {
                throw new BadRequestException($"批次内存在重复定价键: {duplicate.Key}");
            }

            return normalized;
        }

        private static ECoinPrice Normalize(ECoinPriceBatchItemRequest request)
        {
            if (!Enum.IsDefined(request.TargetType))
            {
                throw new BadRequestException("TargetType 无效");
            }
            if (!Enum.IsDefined(request.ChargeType))
            {
                throw new BadRequestException("ChargeType 无效");
            }
            if (request.Price < 0)
            {
                throw new BadRequestException("价格不能为负数");
            }

            var isPlugin = request.TargetType == ECoinTargetType.Plugin;
            var featureId = isPlugin ? request.FeatureId?.Trim() : request.TargetType.ToString();
            var pluginId = isPlugin ? request.PluginId?.Trim() : string.Empty;
            if (isPlugin && (string.IsNullOrWhiteSpace(featureId) || string.IsNullOrWhiteSpace(pluginId)))
            {
                throw new BadRequestException("Plugin 定价必须提供 PluginId 和 FeatureId");
            }

            return new ECoinPrice
            {
                TargetType = request.TargetType,
                FeatureId = featureId ?? string.Empty,
                FeatureDesc = request.FeatureDesc?.Trim() ?? string.Empty,
                Price = request.Price,
                ChargeType = request.ChargeType,
                PluginId = pluginId ?? string.Empty
            };
        }
    }
}
