using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;
using EIMSNext.Entities;

using HKH.Mef2.Integration;
using Microsoft.EntityFrameworkCore;

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

        protected override async Task AfterAdd(IEnumerable<WorkbenchRecentVisit> entities)
        {
            await base.AfterAdd(entities);
            foreach (var employee in entities
                .Where(x => !string.IsNullOrWhiteSpace(x.EmployeeId))
                .Select(x => new { x.CorpId, x.EmployeeId })
                .Distinct())
            {
                await PruneRecentVisitsAsync(employee.CorpId, employee.EmployeeId);
            }
        }

        /// <summary>
        /// 更新（或插入后更新）最近访问记录，VisitCount 自增。
        /// </summary>
        /// <param name="entity">最近访问实体。</param>
        /// <returns>受影响行数。</returns>
        public async Task<int> TouchRecentVisitAsync(WorkbenchRecentVisit entity)
        {
            // 与 Mongo 时期一致：该操作自带隐式事务，不参与外层环境事务。
            using var suppression = TransactionScope.SuppressAmbient();
            var now = DateTime.UtcNow.ToTimeStampMs();
            var op = Context.Operator;

            await BeforeReplace(entity);
            var old = Repository.Queryable
                .FirstOrDefault(x => x.Id == entity.Id
                    && x.CorpId == entity.CorpId
                    && x.EmployeeId == entity.EmployeeId
                    && !x.DeleteFlag);
            if (old is null)
            {
                return 0;
            }

            var affected = await Repository.UpdateManyAsync(
                x => x.Id == entity.Id
                    && x.CorpId == entity.CorpId
                    && x.EmployeeId == entity.EmployeeId
                    && !x.DeleteFlag,
                setters => setters
                    .SetProperty(x => x.TargetType, entity.TargetType)
                    .SetProperty(x => x.TargetId, entity.TargetId)
                    .SetProperty(x => x.AppId, entity.AppId)
                    .SetProperty(x => x.Title, entity.Title)
                    .SetProperty(x => x.Icon, entity.Icon)
                    .SetProperty(x => x.IconColor, entity.IconColor)
                    .SetProperty(x => x.VisitCount, x => x.VisitCount + 1)
                    .SetProperty(x => x.LastVisitTime, now)
                    .SetProperty(x => x.UpdateBy, op)
                    .SetProperty(x => x.UpdateTime, now));
            if (affected == 0)
            {
                return 0;
            }

            // 回读最新状态供审计与后续裁剪使用。
            var updated = Repository.Queryable.FirstOrDefault(x => x.Id == entity.Id && !x.DeleteFlag);
            if (updated is not null)
            {
                updated.CopyTo(entity);
            }

            CreateAuditLog(DbAction.Update, old is null ? null : [old], updated is null ? null : [updated]);
            await AfterReplace(entity);
            await PruneRecentVisitsAsync(entity.CorpId, entity.EmployeeId);
            return affected;
        }

        private async Task PruneRecentVisitsAsync(string? corpId, string employeeId)
        {
            var records = Repository.Queryable
                .Where(x => x.CorpId == corpId && x.EmployeeId == employeeId && !x.DeleteFlag)
                .OrderByDescending(x => x.LastVisitTime)
                .ThenByDescending(x => x.CreateTime)
                .ToList();

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
                await Repository.DeleteManyAsync(x => idsToDelete.Contains(x.Id));
            }
        }
    }
}
