using HKH.Mef2.Integration;

using EIMSNext.Core;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Contracts;

namespace EIMSNext.ApiService
{
	public class DfRunLogNodeApiService(IResolver resolver) : ApiServiceBase<Df_RunLogNode, DfRunLogNodeViewModel, IDfRunLogNodeService>(resolver)
	{
        protected override IQueryable<DfRunLogNodeViewModel> FilterByPermission()
        {
            var query = base.FilterByPermission();
            var evaluator = Resolver.Resolve<AdminPermissionEvaluator>();
            if (evaluator.HasUnrestrictedManagementIdentity)
            {
                return query;
            }

            if (IdentityContext.IdentityType == IdentityType.AppAdmin)
            {
                var appIds = evaluator.GetSnapshot().ManageableAppIds;
                var runLogIds = Resolver.GetService<Df_RunLog>()
                    .Query(x =>
                        x.CorpId == IdentityContext.CurrentCorpId &&
                        !x.DeleteFlag &&
                        appIds.Contains(x.AppId))
                    .Select(x => x.Id)
                    .ToList();

                return query.Where(x => runLogIds.Contains(x.RunLogId));
            }

            return query.Where(x => false);
        }
    }
}
