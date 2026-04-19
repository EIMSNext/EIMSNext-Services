using EIMSNext.Core.Services;
using EIMSNext.Service.Entities;

namespace EIMSNext.Service.Contracts
{
    public interface ISystemMessageService : IService<SystemMessage>
    {
        Task<long> GetUnreadCountAsync(string empId);
        Task MarkReadAsync(string id);
        Task MarkReadBatchAsync(IEnumerable<string> ids);
    }
}
