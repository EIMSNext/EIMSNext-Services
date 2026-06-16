using EIMSNext.ApiService.RequestModels;
using EIMSNext.Core;
using EIMSNext.Core.Repositories;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;

namespace EIMSNext.ApiService
{
    public class AppStoreApiService(IResolver resolver) : ApiServiceBase(resolver)
    {
        private readonly IRepository<AppProfile> _appProfileRepository = resolver.GetRepository<AppProfile>();

        public (long Total, IReadOnlyList<AppProfile> Items) GetAppStore(AppProfileQueryRequest request)
        {
            var query = _appProfileRepository.Queryable.Where(x => !x.DeleteFlag);

            if (!string.IsNullOrWhiteSpace(request.Keyword))
            {
                query = query.Where(x => x.Name.Contains(request.Keyword) || x.Summary.Contains(request.Keyword) || x.Tags.Contains(request.Keyword));
            }

            if (!string.IsNullOrWhiteSpace(request.Category))
            {
                query = query.Where(x => x.Category == request.Category);
            }

            if (!string.IsNullOrWhiteSpace(request.Industry))
            {
                query = query.Where(x => x.Industry == request.Industry);
            }

            if (request.Recommended == true)
            {
                query = query.Where(x => x.IsRecommended);
            }

            var total = query.Count();
            var items = query
                .OrderByDescending(x => x.IsRecommended)
                .ThenByDescending(x => x.SortIndex)
                .Skip(Math.Max(0, request.Skip))
                .Take(Math.Clamp(request.Take <= 0 ? 24 : request.Take, 1, 100))
                .ToList();

            return (total, items);
        }

        public AppProfile? GetAppStoreDetail(string id)
        {
            var profile = _appProfileRepository.Get(id);
            return profile == null || profile.DeleteFlag ? null : profile;
        }
    }
}
