using EIMSNext.ApiClient.Flow;
using EIMSNext.Core;
using EIMSNext.Core.Service;
using EIMSNext.Entity;
using EIMSNext.Service.Interface;
using HKH.Common;
using HKH.Mef2.Integration;
using MongoDB.Driver;

namespace EIMSNext.Service
{
    public class FormDataService : EntityServiceBase<FormData>, IFormDataService
    {
        private FlowApiClient _flowClient;
        public FormDataService(IResolver resolver) : base(resolver)
        {
            _flowClient = resolver.Resolve<FlowApiClient>();
        }

        protected override Task BeforeAdd(IEnumerable<FormData> entities, IClientSessionHandle? session)
        {
            var formDef = GetFromStore<FormDef>(entities.First().FormId)!;
            if (!formDef.UsingWorkflow)
            {
                //非流程单据直接生效
                entities.ForEach(entity => { entity.FlowStatus = FlowStatus.Approved; });
            }
            return base.BeforeAdd(entities, session);
        }

        public override async Task AddAsync(IEnumerable<FormData> entities)
        {
            await base.AddAsync(entities);
            await SubmitAsync(entities, null, Entity.CascadeMode.NotSet, null);
        }

        public override async Task<ReplaceOneResult> ReplaceAsync(FormData entity)
        {
            var result = await base.ReplaceAsync(entity);
            await SubmitAsync([entity], null, Entity.CascadeMode.NotSet, null);
            return result;
        }

        public async Task SubmitAsync(IEnumerable<FormData> entities, IClientSessionHandle? session, Entity.CascadeMode cascade, string? eventIds)
        {
            var entity = entities.First();

            if (Context.Action == Core.Entity.DataAction.Submit)
            {
                var formDef = GetFromStore<FormDef>(entity.FormId)!;

                if (formDef.UsingWorkflow)
                {
                    var wfDef = Resolver.GetRepository<Wf_Definition>().Find(x => x.ExternalId == entity.FormId).FirstOrDefault();
                    if (wfDef != null)
                    {
                        var wfResp = await _flowClient.Start(new StartRequest { WfDefinitionId = entity.FormId, DataId = entity.Id }, Context.AccessToken);
                        if (wfResp != null && !string.IsNullOrEmpty(wfResp.Error))
                        {
                            throw new UnLogException(wfResp.Error);
                        }
                    }
                }
                else
                {
                    if (cascade != Entity.CascadeMode.Never)
                    {
                        //非流程单据直接提交
                        var dfResp = await _flowClient.RunDataflow(new DfRunRequest { DataId = entity.Id, Trigger = DfTrigger.Submit }, Context.AccessToken);
                        if (dfResp != null && !string.IsNullOrEmpty(dfResp.Error))
                        {
                            throw new UnLogException(dfResp.Error);
                        }
                    }
                }
            }
        }
    }
}
