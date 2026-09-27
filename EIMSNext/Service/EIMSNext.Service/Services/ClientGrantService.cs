using HKH.Mef2.Integration;
using EIMSNext.Core.Services;
using EIMSNext.Entities;
using EIMSNext.Service.Contracts;

namespace EIMSNext.Service
{
    /// <summary>
    /// <see cref="ClientGrant"/> 实体服务。
    /// 走 Mongo 的标准 CRUD + <c>LogicDelete = false</c>（与 FormDataPermissionGroup 一致：物理删除）。
    /// </summary>
    public class ClientGrantService(IResolver resolver) : EntityServiceBase<ClientGrant>(resolver), IClientGrantService
    {
        protected override bool LogicDelete => false;
    }
}
