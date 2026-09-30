using EIMSNext.Common;
using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;
using EIMSNext.Entities;
using HKH.Mef2.Integration;
using Microsoft.EntityFrameworkCore;

namespace EIMSNext.Service;

public sealed class CorporateSettingService(IResolver resolver)
    : EntityServiceBase<CorporateSetting>(resolver), ICorporateSettingService
{
    protected override Task BeforeAdd(IEnumerable<CorporateSetting> entities)
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
            if (Exists(setting.CorpId!, setting.Name))
            {
                throw new BadRequestException($"企业配置已存在: {setting.CorpId}/{setting.Name}");
            }
        }

        return Task.CompletedTask;
    }

    protected override Task BeforeReplace(CorporateSetting entity)
    {
        Normalize(entity);
        if (Exists(entity.CorpId!, entity.Name, entity.Id))
        {
            throw new BadRequestException($"企业配置已存在: {entity.CorpId}/{entity.Name}");
        }

        return Task.CompletedTask;
    }

    private bool Exists(string corpId, string name, string? excludedId = null)
    {
        return Repository.Queryable.Any(x =>
            x.CorpId == corpId
            && x.Name == name
            && !x.DeleteFlag
            && (excludedId == null || x.Id != excludedId));
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
            return string.Equals(x.CorpId, y.CorpId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode((string? CorpId, string Name) obj)
        {
            // 必须与上面 Equals 的 IgnoreCase 语义一致，否则 Distinct/HashSet 会先按哈希分流、
            // 根本走不到 Equals，去重结果不确定。
            return HashCode.Combine(
                obj.CorpId is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(obj.CorpId),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name));
        }
    }
}
