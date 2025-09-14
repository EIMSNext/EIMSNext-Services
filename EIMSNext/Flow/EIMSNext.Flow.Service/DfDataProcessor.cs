using EIMSNext.Core;
using EIMSNext.Core.Repository;
using EIMSNext.Entity;
using EIMSNext.Flow.Core;
using EIMSNext.Flow.Core.Interface;
using EIMSNext.Service.Interface;
using HKH.Mef2.Integration;
using Microsoft.Extensions.Logging;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Service
{
    public class DfDataProcessor : IDfDataProcessor
    {
        protected IResolver Resolver { get; private set; }
        protected IRepository<Wf_Definition> WfDefinitionRepository { get; private set; }
        protected IFormDataService FormDataService { get; private set; }
        protected IWorkflowHost WorkflowHost { get; private set; }
        protected ILogger<DfDataProcessor> Logger { get; private set; }

        public DfDataProcessor(IResolver resolver)
        {
            this.Resolver = resolver;
            this.WfDefinitionRepository = resolver.GetRepository<Wf_Definition>();
            this.FormDataService = resolver.Resolve<IFormDataService>();
            this.WorkflowHost = resolver.Resolve<IWorkflowHost>();
            this.Logger = resolver.GetLogger<DfDataProcessor>();
        }

        public void Process(WorkflowInstance inst)
        {
            List<FormData> inserted = new List<FormData>();
            var dataContext = (DfDataContext)inst.Data;

            using (var scope = WfDefinitionRepository.NewTransactionScope())
            {
                //Process Data
                List<FormData> removed = new List<FormData>();
                List<FormData> modified = new List<FormData>();

                dataContext.NodeDatas.Values.ForEach(data =>
                {
                    data.ActionDatas.ForEach(x =>
                    {
                        switch (x.State)
                        {
                            case DataState.Inserted:
                                inserted.Add(x.FormData);
                                break;
                            case DataState.Modified:
                                modified.Add(x.FormData);
                                break;
                            case DataState.Removed:
                                removed.Add(x.FormData);
                                break;
                        }
                    });
                });

                //TODO: 此处应该调用Service, 以处理业务，待重构
                if (removed.Any()) { FormDataService.Delete(removed.Select(x => x.Id)); }
                modified.ForEach(x => { FormDataService.Replace(x); });
                if (inserted.Any())
                {
                    FormDataService.Add(inserted);
                }

                scope.CommitTransaction();
            }

            //TODO: 流程表单，在创建后应自动提交
            inserted.ForEach(x =>
            {
                try
                {
                    var formDef = dataContext.FormDefs[x.FormId];
                    if (formDef.UsingWorkflow)
                    {
                        var data = new WfDataContext(x.CorpId ?? "", x.AppId, x.FormId, x.Id, x.CreateBy, dataContext.DfCascade, dataContext.EventIds);
                        WorkflowHost.StartWorkflow(x.FormId, data.ToExpando());
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, $"Dataflow自动提交表单失败：{x.Id}");
                }
            });
        }
    }
}
