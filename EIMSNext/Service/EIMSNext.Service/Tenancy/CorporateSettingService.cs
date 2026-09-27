using EIMSNext.Common;
using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;
using EIMSNext.Entities;
using HKH.Mef2.Integration;
using MongoDB.Driver;

namespace EIMSNext.Service;

public sealed class CorporateSettingService(IResolver resolver)
    : EntityServiceBase<CorporateSetting>(resolver), ICorporateSettingService
{
    protected override Task BeforeAdd(IEnumerable<CorporateSetting> entities, IClientSessionHandle? session)
    {
        var settings = entities.ToList();
        foreach (var setting in settings)
        {
            Normalize(setting);
        }

        var duplicate = settings
            .GroupBy(x => (x.CorpId, x.Name), StringTupleComparer.Instance)
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicate != null)
        {
            throw new BadRequestException($"企业配置重复: {duplicate.Key.CorpId}/{duplicate.Key.Name}");
        }

        foreach (var setting in settings)
        {
            if (Exists(setting.CorpId!, setting.Name, session))
            {
                throw new BadRequestException($"企业配置已存在: {setting.CorpId}/{setting.Name}");
            }
        }

        return Task.CompletedTask;
    }

    protected override Task BeforeReplace(CorporateSetting entity, IClientSessionHandle? session)
    {
        Normalize(entity);
        if (Exists(entity.CorpId!, entity.Name, session, entity.Id))
        {
            throw new BadRequestException($"企业配置已存在: {entity.CorpId}/{entity.Name}");
        }

        return Task.CompletedTask;
    }

    private bool Exists(string corpId, string name, IClientSessionHandle? session, string? excludedId = null)
    {
        var filter = Builders<CorporateSetting>.Filter.And(
            Builders<CorporateSetting>.Filter.Eq(x => x.CorpId, corpId),
            Builders<CorporateSetting>.Filter.Eq(x => x.Name, name),
            Builders<CorporateSetting>.Filter.Eq(x => x.DeleteFlag, false),
            excludedId == null
                ? Builders<CorporateSetting>.Filter.Empty
                : Builders<CorporateSetting>.Filter.Ne(x => x.Id, excludedId));

        return session == null
            ? Collection.Find(filter).Limit(1).Any()
            : Collection.Find(session, filter).Limit(1).Any();
    }

    private static void Normalize(CorporateSetting setting)
    {
        setting.Name = setting.Name.Trim();
        setting.Value ??= string.Empty;
        setting.Desc ??= string.Empty;

        if (string.IsNullOrWhiteSpace(setting.CorpId))
        {
            throw new BadRequestException("企业配置必须指定企业");
        }

        if (string.IsNullOrWhiteSpace(setting.Name))
        {
            throw new BadRequestException("企业配置名称不能为空");
        }
    }

    private sealed class StringTupleComparer : IEqualityComparer<(string? CorpId, string Name)>
    {
        public static readonly StringTupleComparer Instance = new();

        public bool Equals((string? CorpId, string Name) x, (string? CorpId, string Name) y)
        {
            return string.Equals(x.CorpId, y.CorpId, StringComparison.Ordinal)
                && string.Equals(x.Name, y.Name, StringComparison.Ordinal);
        }

        public int GetHashCode((string? CorpId, string Name) obj)
        {
            return HashCode.Combine(obj.CorpId, obj.Name);
        }
    }
}
