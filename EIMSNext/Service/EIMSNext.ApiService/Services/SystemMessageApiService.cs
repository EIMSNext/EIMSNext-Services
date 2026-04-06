using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using EIMSNext.ApiService.ViewModels;

using HKH.Mef2.Integration;

namespace EIMSNext.ApiService
{
    public class SystemMessageApiService(IResolver resolver) : ApiServiceBase<SystemMessage, SystemMessageViewModel, ISystemMessageService>(resolver)
    {
        public Task<long> GetUnreadCountAsync()
        {
            return CoreService.GetUnreadCountAsync(GetCurrentEmpId());
        }

        public Task MarkReadAsync(string id)
        {
            return CoreService.MarkReadAsync(id, GetCurrentEmpId());
        }

        public Task MarkReadBatchAsync(IEnumerable<string> ids)
        {
            return CoreService.MarkReadBatchAsync(ids, GetCurrentEmpId());
        }

        protected override IQueryable<SystemMessageViewModel> FilterByPermission()
        {
            var empId = GetCurrentEmpId();
            return base.FilterByPermission().Where(x => x.ReceiverEmpId == empId);
        }

        private string GetCurrentEmpId()
        {
            return IdentityContext.CurrentEmployee?.Id ?? string.Empty;
        }
    }
}
