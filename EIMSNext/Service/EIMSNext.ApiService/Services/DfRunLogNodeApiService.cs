using HKH.Mef2.Integration;

using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Contracts;

using MongoDB.Driver;

namespace EIMSNext.ApiService
{
	public class DfRunLogNodeApiService(IResolver resolver) : ApiServiceBase<Df_RunLogNode, DfRunLogNodeViewModel, IDfRunLogNodeService>(resolver)
	{
        // 不再预过滤 AppId/RunLogId：
        // 1) Df_RunLogNode 实际查询路径是通过 OData $filter=RunLogId eq 'xxx' 限定到单次运行，
        //    业务侧在调用时已知道 RunLog 归属，无需服务端再按 AppId 反查。
        // 2) 企业隔离由 base.FilterByPermission（FilterByCorpId）兜底。
        // 3) 跳过 AppAdmin 的 RunLogId 预取，避免 ToList 内存物化。
        // 备注：MongoDB.Driver LINQ 不支持子查询翻译，要做"RunLogId in (select Id from Df_RunLog)"
        // 需走聚合管道 $lookup。如未来需要此层隔离，按此模式重写。
    }
}
