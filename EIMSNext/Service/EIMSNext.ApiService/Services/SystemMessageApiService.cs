using EIMSNext.Service.Contracts;
using EIMSNext.Entities;
using EIMSNext.ApiService.ViewModels;

using HKH.Mef2.Integration;

namespace EIMSNext.ApiService
{
    /// <summary>
    /// 系统消息的 API 服务。
    /// </summary>
    /// <param name="resolver">服务解析器。</param>
    public class SystemMessageApiService(IResolver resolver) : ApiServiceBase<SystemMessage, SystemMessageViewModel, ISystemMessageService>(resolver)
    {
        /// <summary>
        /// 获取未读消息数量。
        /// </summary>
        public Task<long> GetUnreadCountAsync()
        {
            return CoreService.GetUnreadCountAsync(GetCurrentEmpId());
        }

        /// <summary>
        /// 标记消息为已读。
        /// </summary>
        public Task MarkReadAsync(string id)
        {
            return CoreService.MarkReadAsync(id, IdentityContext.CurrentCorpId, GetCurrentEmpId());
        }

        /// <summary>
        /// 批量标记消息为已读。
        /// </summary>
        public Task MarkReadBatchAsync(IEnumerable<string> ids)
        {
            return CoreService.MarkReadBatchAsync(ids, IdentityContext.CurrentCorpId, GetCurrentEmpId());
        }

        /// <summary>
        /// 按当前身份权限过滤查询。
        /// </summary>
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
