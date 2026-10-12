using System;
using System.Collections.Generic;
using System.Linq;

using Asp.Versioning;

using EIMSNext.ApiService;
using EIMSNext.Common;
using EIMSNext.Entities;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Host.Authorization;

using HKH.Mef2.Integration;

using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.Service.Host.Controllers;

/// <summary>
/// 企业配置的普通用户读取接口。
/// OData 侧 <c>CorporateSetting</c> 仅管理员可用，这里为登录后的普通用户暴露可公开的配置子集。
/// </summary>
/// <param name="resolver">对象容器。</param>
[ApiVersion(1.0)]
[IdentityType(IdentityTypeDefaults.BusinessUser)]
public sealed class CorporateSettingController(IResolver resolver)
    : ApiControllerBase<CorporateSettingApiService, CorporateSetting>(resolver)
{
    /// <summary>
    /// 允许普通用户读取的企业配置项白名单。
    /// </summary>
    private static readonly string[] PublicSettingNames =
    [
        CorporateSettingNames.ThemeColor,
        CorporateSettingNames.StyleEnabled,
    ];

    /// <summary>
    /// 获取当前企业适用于普通用户的配置集合。
    /// </summary>
    [HttpGet("current")]
    [Permission(Operation = Operation.Read)]
    public ActionResult<IEnumerable<CorporateSettingReadModel>> GetCurrent()
    {
        var corpId = IdentityContext.CurrentCorpId;
        if (string.IsNullOrWhiteSpace(corpId))
        {
            return Ok(Array.Empty<CorporateSettingReadModel>());
        }

        var settings = Resolver.Resolve<ICorporateSettingService>().All()
            .Where(x => x.CorpId == corpId && !x.DeleteFlag && PublicSettingNames.Contains(x.Name))
            .Select(x => new CorporateSettingReadModel
            {
                Id = x.Id,
                CorpId = x.CorpId,
                Name = x.Name,
                Value = x.Value,
                Desc = x.Desc,
            })
            .ToList();

        return Ok(settings);
    }
}
