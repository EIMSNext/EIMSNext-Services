using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;

using HKH.Mef2.Integration;

using MongoDB.Driver;

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

        public Task MarkReadAsync(string id)
        {
            var update = UpdateBuilder
                .Set(x => x.IsRead, true)
                .Set(x => x.ReadTime, DateTime.UtcNow.ToTimeStampMs());

            Repository.Update(id, update, upsert: false);
            return Task.CompletedTask;
        }

        public Task MarkReadBatchAsync(IEnumerable<string> ids)
        {
            var update = UpdateBuilder
                .Set(x => x.IsRead, true)
                .Set(x => x.ReadTime, DateTime.UtcNow.ToTimeStampMs());

            Repository.UpdateMany(Repository.FilterBuilder.In(x => x.Id, ids), update, upsert: false);
            return Task.CompletedTask;
        }
    }
}
