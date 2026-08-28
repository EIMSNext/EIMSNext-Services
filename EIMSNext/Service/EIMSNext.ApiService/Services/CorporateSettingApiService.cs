using EIMSNext.ApiService.ViewModels;
using EIMSNext.Common;
using EIMSNext.Service.Contracts;
using EIMSNext.Entities;
using HKH.Mef2.Integration;
using MongoDB.Driver;

namespace EIMSNext.ApiService;

public sealed class CorporateSettingApiService(IResolver resolver)
    : ApiServiceBase<CorporateSetting, CorporateSettingViewModel, ICorporateSettingService>(resolver)
{
    protected override async Task AddAsyncCore(CorporateSetting entity)
    {
        entity.CorpId = IdentityContext.CurrentCorpId;
        if (string.IsNullOrWhiteSpace(entity.CorpId))
        {
            throw new BadRequestException("当前用户未选择企业");
        }

        await base.AddAsyncCore(entity);
    }

    protected override async Task<ReplaceOneResult> ReplaceAsyncCore(CorporateSetting entity)
    {
        var existing = await CoreService.GetAsync(entity.Id);
        if (existing == null || existing.DeleteFlag || existing.CorpId != IdentityContext.CurrentCorpId)
        {
            throw new BadRequestException("企业配置不存在");
        }

        entity.Id = existing.Id;
        entity.CorpId = existing.CorpId;
        entity.CreateBy = existing.CreateBy;
        entity.CreateTime = existing.CreateTime;
        return await base.ReplaceAsyncCore(entity);
    }

    protected override async Task<object> DeleteAsyncCore(IEnumerable<string> ids)
    {
        var idList = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
        var existing = CoreService.All()
            .Where(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag && idList.Contains(x.Id))
            .Select(x => x.Id)
            .ToList();

        if (existing.Count != idList.Count)
        {
            throw new BadRequestException("企业配置不存在");
        }

        return await base.DeleteAsyncCore(existing);
    }
}
