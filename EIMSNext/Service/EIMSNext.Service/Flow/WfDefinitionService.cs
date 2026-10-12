using System.Linq.Expressions;
using HKH.Mef2.Integration;

using EIMSNext.Common.Extensions;
using EIMSNext.Component;
using EIMSNext.Core.Query;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Services;
using EIMSNext.Entities;
using EIMSNext.Service.Contracts;

using Microsoft.EntityFrameworkCore;

namespace EIMSNext.Service
{
    public class WfDefinitionService : EntityServiceBase<Wf_Definition>, IWfDefinitionService
    {
        private WfMetadataParser metadataParser;
        private readonly IEventFlowScheduleService _eventFlowScheduleService;

        public WfDefinitionService(IResolver resolver) : base(resolver)
        {
            metadataParser = resolver.Resolve<WfMetadataParser>();
            _eventFlowScheduleService = resolver.Resolve<IEventFlowScheduleService>();
        }

        public Wf_Definition? Find(string wfExternalId, int? version = null)
        {
            if (version.HasValue)
            {
                return FindCore(x => x.ExternalId == wfExternalId && x.Version == version.Value).FirstOrDefault();
            }
            else
            {
                return FindCore(x => x.ExternalId == wfExternalId && x.IsCurrent).FirstOrDefault();
            }
        }

        public async Task<Wf_Definition> CreateVersionAsync(string id)
        {
            var source = (await GetCoreAsync(id).ConfigureAwait(false)) ?? throw new InvalidOperationException("源流程版本不存在");
            var entity = new Wf_Definition
            {
                AppId = source.AppId,
                Name = source.Name,
                FlowType = source.FlowType,
                ExternalId = source.ExternalId,
                Description = source.Description,
                Content = source.Content,
                EventSource = source.EventSource,
                SourceId = source.SourceId,
                Disabled = source.Disabled,
            };

            await AddAsync(entity);
            return entity;
        }

        public async Task<Wf_Definition> ActivateAsync(string id)
        {
            return await ExecuteWithTransactionRetryAsync(async () =>
            {
                var entity = (await GetCoreAsync(id).ConfigureAwait(false)) ?? throw new InvalidOperationException("流程版本不存在");
                // 同一 ExternalId 下只会有一个 IsCurrent 版本，切换前先把旧版本落下去。
                await Repository.UpdateManyAsync(
                    x => x.ExternalId == entity.ExternalId && x.IsCurrent && x.Id != entity.Id,
                    setters => setters.SetProperty(x => x.IsCurrent, false));

                entity.IsCurrent = true;
                entity.Released = true;
                await ReplaceAsync(entity);
                return entity;
            }).ConfigureAwait(false);
        }

        protected override async Task BeforeAdd(IEnumerable<Wf_Definition> entities)
        {
            var entity = entities.First();

            var content = metadataParser.Parse(entity);
            entity.Metadata = content.Metadata;
            if (entity.FlowType == FlowType.EventFlow)
            {
                //数据流有多种流程定义，所以不使用FormId
                entity.ExternalId = Repository.NewId();
                entity.Metadata.Id = entity.ExternalId;
                entity.EventSetting = content.EventSetting;
            }

            EnsureSourceId(entity);

            var maxVersion = await Repository.Queryable
                .Where(x => x.ExternalId == entity.ExternalId && !x.DeleteFlag)
                .Select(x => (int?)x.Version)
                .MaxAsync()
                .ConfigureAwait(false) ?? 0;

            entity.Version = maxVersion + 1;
            entity.Metadata.Id = entity.ExternalId;
            entity.Metadata.Version = entity.Version;
            entity.IsCurrent = false;
            entity.Released = false;
        }

        protected override async Task BeforeReplace(Wf_Definition entity)
        {
            var exist = (await GetCoreAsync(entity.Id).ConfigureAwait(false)) ?? throw new InvalidOperationException("流程版本不存在");

            entity.Version = exist.Version;
            if (exist.Released) //release 不允许往回改
                entity.Released = exist.Released;

            var content = metadataParser.Parse(entity);
            entity.Metadata = content.Metadata;
            if (entity.FlowType == FlowType.EventFlow)
            {
                entity.EventSetting = content.EventSetting;
            }

            EnsureSourceId(entity);
        }

        protected override async Task AfterAdd(IEnumerable<Wf_Definition> entities)
        {
            foreach (var entity in entities.Where(x => x.FlowType == FlowType.EventFlow))
            {
                await _eventFlowScheduleService.RebuildScheduleAsync(entity);
            }

            await base.AfterAdd(entities);
        }

        protected override async Task AfterReplace(Wf_Definition entity)
        {
            if (entity.FlowType == FlowType.EventFlow)
            {
                await _eventFlowScheduleService.RebuildScheduleAsync(entity);
            }

            await base.AfterReplace(entity);
        }

        protected override async Task BeforeDelete(Expression<Func<Wf_Definition, bool>> filter)
        {
            if (await Repository.Queryable.Where(filter).AnyAsync(x => x.Released).ConfigureAwait(false))
            {
                throw new InvalidOperationException("已启用或历史版本不允许删除");
            }
        }

        /// <summary>
        /// 绕过 <see cref="BeforeDelete"/> 的已发布校验删除定义。
        /// </summary>
        /// <remarks>
        /// 删应用/删表单的级联清理会走到这里：主体已不存在，挂在其下的定义（含已发布版本）必须一并清掉，
        /// 否则会留下永远删不掉的孤儿定义。
        /// </remarks>
        public async Task<int> DeleteForceAsync(IEnumerable<string> ids)
        {
            var idList = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (idList.Count == 0)
            {
                return 0;
            }

            return await Repository.SoftDeleteManyAsync(
                x => idList.Contains(x.Id), Context.Operator, DateTime.UtcNow.ToTimeStampMs());
        }

        /// <summary>
        /// 工作流的 <see cref="Wf_Definition.ExternalId"/> 就是表单Id。删表单时按 SourceId 反查定义，
        /// 这里必须保证它落值，否则级联清理永远匹配不到。
        /// </summary>
        private static void EnsureSourceId(Wf_Definition entity)
        {
            if (entity.FlowType == FlowType.Workflow && string.IsNullOrWhiteSpace(entity.SourceId))
            {
                entity.SourceId = entity.ExternalId;
            }
        }
    }
}
