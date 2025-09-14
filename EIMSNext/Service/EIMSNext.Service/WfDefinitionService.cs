using System.Text.Json;

using HKH.Mef2.Integration;

using EIMSNext.Component;
using EIMSNext.Core.Query;
using EIMSNext.Core.Service;
using EIMSNext.Entity;
using EIMSNext.Service.Interface;

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

        protected override Task BeforeAdd(IEnumerable<Wf_Definition> entities, IClientSessionHandle? session)
        {
            var entity = entities.First();

            var content = metadataParser.Parse(entity);
            entity.Metadata = content.Metadata;
            entity.IsCurrent = true;
            if (entity.FlowType == FlowType.Dataflow)
            {
                //数据流有多种流程定义，所以不使用FormId
                entity.ExternalId = Repository.NewId();
                entity.Metadata.Id = entity.ExternalId;
                entity.EventSetting = content.EventSetting;
            }

            if (entity.Version > 1)
            {
                var filter = FilterBuilder.Eq(x => x.ExternalId, entity.ExternalId);
                var update = UpdateBuilder.Set(x => x.IsCurrent, false);
                Repository.UpdateMany(filter, update, session: session);
            }

            return Task.CompletedTask;
        }

        protected override Task BeforeReplace(Wf_Definition entity, IClientSessionHandle? session)
        {
            var content = metadataParser.Parse(entity);
            entity.Metadata = content.Metadata;
            if (entity.FlowType == FlowType.Dataflow)
            {
                entity.EventSetting = content.EventSetting;
            }
            return Task.CompletedTask;
        }
    }
}
