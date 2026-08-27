using HKH.Mef2.Integration;
using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Core.Services;
using EIMSNext.Entities;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Contracts;
using MongoDB.Driver;

namespace EIMSNext.ApiService
{
	public class EmployeeGroupCategoryApiService(IResolver resolver) : ApiServiceBase<EmployeeGroupCategory, EmployeeGroupCategoryViewModel, IEmployeeGroupCategoryService>(resolver)
	{
        protected override Task AddAsyncCore(EmployeeGroupCategory entity)
        {
            Resolver.Resolve<TenantAccessEvaluator>().EnsureUnrestrictedManagement("没有创建角色组的权限");
            return base.AddAsyncCore(entity);
        }

        protected override Task<ReplaceOneResult> ReplaceAsyncCore(EmployeeGroupCategory entity)
        {
            Resolver.Resolve<TenantAccessEvaluator>().EnsureUnrestrictedManagement("没有修改角色组的权限");
            return base.ReplaceAsyncCore(entity);
        }

        protected override async Task<object> DeleteAsyncCore(IEnumerable<string> ids)
        {
            Resolver.Resolve<TenantAccessEvaluator>().EnsureUnrestrictedManagement("没有删除角色组的权限");
            var idList = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            if (idList.Count > 0)
            {
                var hasRoles = Resolver.GetService<EmployeeGroup>().All().Any(x =>
                    x.CorpId == IdentityContext.CurrentCorpId &&
                    !x.DeleteFlag &&
                    idList.Contains(x.EmployeeGroupCategoryId));

                if (hasRoles)
                {
                    throw new BadRequestException("角色组下存在角色，不能删除");
                }
            }

            return await base.DeleteAsyncCore(idList);
        }
	}
}
