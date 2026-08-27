using Asp.Versioning;

using HKH.Mef2.Integration;

using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Service.Host.OData;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Entities;
using EIMSNext.ApiService;

using Microsoft.AspNetCore.OData.Query;

using MongoDB.Driver;

namespace EIMSNext.Service.Host.Controllers.OData
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
	public class EfRunLogNodeController(IResolver resolver) : ReadOnlyODataController<EfRunLogNodeApiService, Ef_RunLogNode, EfRunLogNodeViewModel>(resolver)
	{
        // 业务侧通常通过 OData $filter=RunLogId eq 'xxx' 限定到单次运行；AppId 隔离由
        // EfRunLogApiService.GetRunDetailAsync 中 EnsureCanManageEventFlow / EnsureCanManageApp 守住。
        // 此处仅依赖 base.FilterResult（FilterByCorpId + FilterByDeleted）做企业隔离，
        // 不再预过滤 AppId/RunLogId，避免 ToList 内存物化。
	}
}
