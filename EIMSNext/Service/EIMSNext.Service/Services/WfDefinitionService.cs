using HKH.Mef2.Integration;

using EIMSNext.Component;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Contracts;

using MongoDB.Driver;

namespace EIMSNext.Service
{
    public class WfDefinitionService : EntityServiceBase<Wf_Definition>, IWfDefinitionService
    {
        private WfMetadataParser metadataParser;

        public WfDefinitionService(IResolver resolver) : base(resolver)
        {
            metadataParser = resolver.Resolve<WfMetadataParser>();
        }

        public Wf_Definition? Find(string wfExternalId, int? version = null)
        {
            if (version.HasValue)
            {
                return FindCore(x => x.ExternalId == wfExternalId && x.Version == version.Value, null).FirstOrDefault();
            }
            else
            {
                return FindCore(x => x.ExternalId == wfExternalId && x.IsCurrent, null).FirstOrDefault();
            }
        }

        public async Task<Wf_Definition> CreateVersionAsync(string id)
        {
            var source = Get(id) ?? throw new InvalidOperationException("源流程版本不存在");
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
            var entity = Get(id) ?? throw new InvalidOperationException("流程版本不存在");

            Repository.UpdateMany(
                FilterBuilder.Eq(x => x.ExternalId, entity.ExternalId),
                UpdateBuilder.Set(x => x.IsCurrent, false));

            entity.IsCurrent = true;
            entity.Released = true;
            await ReplaceAsync(entity);
            return entity;
        }

        protected override Task BeforeAdd(IEnumerable<Wf_Definition> entities, IClientSessionHandle? session)
        {
            var entity = entities.First();

            var content = metadataParser.Parse(entity);
            entity.Metadata = content.Metadata;
            if (entity.FlowType == FlowType.Dataflow)
            {
                //数据流有多种流程定义，所以不使用FormId
                entity.ExternalId = Repository.NewId();
                entity.Metadata.Id = entity.ExternalId;
                entity.EventSetting = content.EventSetting;
            }

            var maxVersion = Query(x => x.ExternalId == entity.ExternalId && !x.DeleteFlag)
                .Select(x => x.Version)
                .DefaultIfEmpty(0)
                .Max();

            entity.Version = maxVersion + 1;
            entity.IsCurrent = false;
            entity.Released = false;

            return Task.CompletedTask;
        }

        protected override Task BeforeReplace(Wf_Definition entity, IClientSessionHandle? session)
        {
            var exist = Get(entity.Id) ?? throw new InvalidOperationException("流程版本不存在");

            entity.Version = exist.Version;
            entity.IsCurrent = exist.IsCurrent;
            entity.Released = exist.Released;

            var content = metadataParser.Parse(entity);
            entity.Metadata = content.Metadata;
            if (entity.FlowType == FlowType.Dataflow)
            {
                entity.EventSetting = content.EventSetting;
            }

            return Task.CompletedTask;
        }

        protected override Task BeforeDelete(FilterDefinition<Wf_Definition> filter, IClientSessionHandle? session)
        {
            var releasedDefs = Repository.Find(new MongoFindOptions<Wf_Definition> { Filter = filter }, session)
                .ToList()
                .Where(x => x.Released)
                .ToList();
            if (releasedDefs.Count > 0)
            {
                throw new InvalidOperationException("已启用或历史版本不允许删除");
            }

            return Task.CompletedTask;
        }
    }
}
