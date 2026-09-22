using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;
using EIMSNext.Entities;

using HKH.Mef2.Integration;

using Microsoft.EntityFrameworkCore;

namespace EIMSNext.Service
{
    public class SystemMessageService(IResolver resolver) : EntityServiceBase<SystemMessage>(resolver), ISystemMessageService
    {
        public Task<long> GetUnreadCountAsync(string empId)
        {
            return CountAsync(new DynamicFilter
            {
                Rel = FilterRel.And,
                Items =
                [
                    new DynamicFilter { Field = nameof(SystemMessage.ReceiverEmpId), Op = FilterOp.Eq, Value = empId },
                    new DynamicFilter { Field = nameof(SystemMessage.IsRead), Op = FilterOp.Eq, Value = false },
                    new DynamicFilter { Field = nameof(SystemMessage.ExpireTime), Op = FilterOp.Gt, Value = DateTime.UtcNow.ToTimeStampMs() }
                ]
            });
        }

        public async Task MarkReadAsync(string id, string corpId, string empId)
        {
            var readTime = DateTime.UtcNow.ToTimeStampMs();
            await Repository.UpdateManyAsync(
                x => x.Id == id
                    && x.CorpId == corpId
                    && x.ReceiverEmpId == empId
                    && !x.DeleteFlag,
                setters => setters
                    .SetProperty(x => x.IsRead, true)
                    .SetProperty(x => x.ReadTime, readTime));
        }

        public async Task MarkReadBatchAsync(IEnumerable<string> ids, string corpId, string empId)
        {
            var idList = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToList();
            if (idList.Count == 0)
            {
                return;
            }

            var readTime = DateTime.UtcNow.ToTimeStampMs();
            await Repository.UpdateManyAsync(
                x => idList.Contains(x.Id)
                    && x.CorpId == corpId
                    && x.ReceiverEmpId == empId
                    && !x.DeleteFlag,
                setters => setters
                    .SetProperty(x => x.IsRead, true)
                    .SetProperty(x => x.ReadTime, readTime));
        }
    }
}
