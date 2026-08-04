using EIMSNext.Core.Services;
using EIMSNext.Service.Entities;

namespace EIMSNext.Service.Contracts
{
    public interface IFormDefService : IService<FormDef>
    {
        Task PurgeFieldChangeLogsAsync(string formId, IReadOnlyCollection<string> fieldIds, bool clearAll);
    }
}
