using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;
using EIMSNext.Entities;

using HKH.Mef2.Integration;
using MongoDB.Driver;

namespace EIMSNext.Service
{
    public class WorkbenchConfigService(IResolver resolver) : EntityServiceBase<WorkbenchConfig>(resolver), IWorkbenchConfigService
    {
    }

    public class WorkbenchFavoriteService(IResolver resolver) : EntityServiceBase<WorkbenchFavorite>(resolver), IWorkbenchFavoriteService
    {
    }

    public class WorkbenchRecentVisitService(IResolver resolver) : EntityServiceBase<WorkbenchRecentVisit>(resolver), IWorkbenchRecentVisitService
    {
        private const int MaxRecentVisitCount = 10;

        protected override bool LogicDelete => false;
        protected override bool TransNeeded => false;

        protected override async Task AfterAdd(IEnumerable<WorkbenchRecentVisit> entities, IClientSessionHandle? session)
        {
            await base.AfterAdd(entities, session);
            foreach (var employee in entities
                .Where(x => !string.IsNullOrWhiteSpace(x.EmployeeId))
                .Select(x => new { x.CorpId, x.EmployeeId })
                .Distinct())
            {
                await PruneRecentVisitsAsync(employee.CorpId, employee.EmployeeId, session);
            }
        }

        public async Task<ReplaceOneResult> TouchRecentVisitAsync(WorkbenchRecentVisit entity)
        {
            using var suppression = MongoTransactionScope.SuppressAmbient();
            var now = DateTime.UtcNow.ToTimeStampMs();
            IClientSessionHandle? session = null;
            var filter = Builders<WorkbenchRecentVisit>.Filter.And(
                Builders<WorkbenchRecentVisit>.Filter.Eq(x => x.Id, entity.Id),
                Builders<WorkbenchRecentVisit>.Filter.Eq(x => x.CorpId, entity.CorpId),
                Builders<WorkbenchRecentVisit>.Filter.Eq(x => x.EmployeeId, entity.EmployeeId),
                Builders<WorkbenchRecentVisit>.Filter.Eq(x => x.DeleteFlag, false));
            var update = Builders<WorkbenchRecentVisit>.Update.Combine(
                Builders<WorkbenchRecentVisit>.Update.Set(x => x.TargetType, entity.TargetType),
                Builders<WorkbenchRecentVisit>.Update.Set(x => x.TargetId, entity.TargetId),
                Builders<WorkbenchRecentVisit>.Update.Set(x => x.AppId, entity.AppId),
                Builders<WorkbenchRecentVisit>.Update.Set(x => x.Title, entity.Title),
                Builders<WorkbenchRecentVisit>.Update.Set(x => x.Icon, entity.Icon),
                Builders<WorkbenchRecentVisit>.Update.Set(x => x.IconColor, entity.IconColor),
                Builders<WorkbenchRecentVisit>.Update.Inc(x => x.VisitCount, 1),
                Builders<WorkbenchRecentVisit>.Update.Set(x => x.LastVisitTime, now),
                Builders<WorkbenchRecentVisit>.Update.Set(x => x.UpdateBy, Context.Operator),
                Builders<WorkbenchRecentVisit>.Update.Set(x => x.UpdateTime, now));
            await BeforeReplace(entity, session);
            var old = await GetRecentVisitAsync(filter, session);
            var options = new FindOneAndUpdateOptions<WorkbenchRecentVisit>
            {
                ReturnDocument = ReturnDocument.After
            };
            var updated = session == null
                ? await Repository.Collection.FindOneAndUpdateAsync(filter, update, options)
                : await Repository.Collection.FindOneAndUpdateAsync(session, filter, update, options);
            if (updated == null)
            {
                return new ReplaceOneResult.Acknowledged(0, 0, null);
            }

            updated.CopyTo(entity);
            CreateAuditLog(DbAction.Update, old == null ? null : [old], [updated], null, null, session);
            await AfterReplace(entity, session);
            await PruneRecentVisitsAsync(entity.CorpId, entity.EmployeeId, session);
            return new ReplaceOneResult.Acknowledged(1, 1, null);
        }

        private async Task<WorkbenchRecentVisit?> GetRecentVisitAsync(
            FilterDefinition<WorkbenchRecentVisit> filter,
            IClientSessionHandle? session)
        {
            return session == null
                ? await Repository.Collection.Find(filter).FirstOrDefaultAsync()
                : await Repository.Collection.Find(session, filter).FirstOrDefaultAsync();
        }

        private async Task PruneRecentVisitsAsync(string? corpId, string employeeId, IClientSessionHandle? session)
        {
            var filter = Builders<WorkbenchRecentVisit>.Filter.And(
                Builders<WorkbenchRecentVisit>.Filter.Eq(x => x.CorpId, corpId),
                Builders<WorkbenchRecentVisit>.Filter.Eq(x => x.EmployeeId, employeeId),
                Builders<WorkbenchRecentVisit>.Filter.Eq(x => x.DeleteFlag, false));
            var records = await (session == null
                ? Repository.Collection.Find(filter)
                : Repository.Collection.Find(session, filter))
                .SortByDescending(x => x.LastVisitTime)
                .ThenByDescending(x => x.CreateTime)
                .ToListAsync();

            var seenTargets = new HashSet<string>();
            var keptCount = 0;
            var idsToDelete = new List<string>();
            foreach (var record in records)
            {
                var targetKey = $"{record.TargetType}:{record.TargetId}";
                if (!seenTargets.Add(targetKey) || keptCount >= MaxRecentVisitCount)
                {
                    idsToDelete.Add(record.Id);
                    continue;
                }

                keptCount++;
            }

            if (idsToDelete.Count > 0)
            {
                var deleteFilter = Builders<WorkbenchRecentVisit>.Filter.In(x => x.Id, idsToDelete);
                if (session == null)
                    await Repository.Collection.DeleteManyAsync(deleteFilter);
                else
                    await Repository.Collection.DeleteManyAsync(session, deleteFilter);
            }
        }
    }
}
