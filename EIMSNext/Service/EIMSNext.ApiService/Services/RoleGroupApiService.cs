using HKH.Mef2.Integration;
using EIMSNext.Common;
using EIMSNext.Core.Services;
using EIMSNext.Service.Entities;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Contracts;
using MongoDB.Driver;

namespace EIMSNext.ApiService
{
	public class RoleGroupApiService(IResolver resolver) : ApiServiceBase<RoleGroup, RoleGroupViewModel, IRoleGroupService>(resolver)
	{
        protected override Task AddAsyncCore(RoleGroup entity)
        {
            Resolver.Resolve<AdminPermissionEvaluator>().EnsureUnrestrictedManagement("没有创建角色组的权限");
            return base.AddAsyncCore(entity);
        }

        protected override Task<ReplaceOneResult> ReplaceAsyncCore(RoleGroup entity)
        {
            Resolver.Resolve<AdminPermissionEvaluator>().EnsureUnrestrictedManagement("没有修改角色组的权限");
            return base.ReplaceAsyncCore(entity);
        }

        protected override Task<object> DeleteAsyncCore(IEnumerable<string> ids)
        {
            Resolver.Resolve<AdminPermissionEvaluator>().EnsureUnrestrictedManagement("没有删除角色组的权限");
            return base.DeleteAsyncCore(ids);
        }
	}
}
