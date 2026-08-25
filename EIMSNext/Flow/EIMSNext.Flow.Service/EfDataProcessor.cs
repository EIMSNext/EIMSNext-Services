using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Service.Entities;
using EIMSNext.Flow.Core;
using EIMSNext.Flow.Core.Interfaces;
using EIMSNext.Service.Contracts;
using HKH.Mef2.Integration;
using Microsoft.Extensions.Logging;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Service
{
    /// <summary>
    /// EventFlow 写数据的入口。
    /// <para>
    /// 重要约束：eventFlow 通过本处理器写入的 <see cref="FormData"/> 使用
    /// <see cref="DataAction.EventFlow"/> 而非 <see cref="DataAction.Submit"/>，
    /// 因此 <see cref="FormDataService.BeforeAdd"/> 中 <c>ResolveSerialNumbers</c>
    /// 不会触发——这意味着 eventFlow 插入的 <c>serialno</c> 类型字段会留空。
    /// </para>
    /// <para>
    /// 这是有意为之：eventFlow 通常用于子表/批量/历史回写，让流水号在子表内自增会与
    /// 主表单号冲突。若业务需要让 eventFlow 写入的记录也带流水号，请在该节点之前的
    /// 公式/赋值中显式计算序列值，或将上游 Action 改为 <c>Submit</c>。
    /// </para>
    /// </summary>
    public class EfDataProcessor : IEfDataProcessor
    {
        protected IResolver Resolver { get; private set; }
        protected IServiceContext ServiceContext { get; private set; }
        protected IRepository<Wf_Definition> WfDefinitionRepository { get; private set; }
        protected IFormDataService FormDataService { get; private set; }
        protected IWorkflowHost WorkflowHost { get; private set; }
        protected ILogger<EfDataProcessor> Logger { get; private set; }

        public EfDataProcessor(IResolver resolver)
        {
            this.Resolver = resolver;
            this.ServiceContext = resolver.GetServiceContext();
            this.WfDefinitionRepository = resolver.GetRepository<Wf_Definition>();
            this.FormDataService = resolver.Resolve<IFormDataService>();
            this.WorkflowHost = resolver.Resolve<IWorkflowHost>();
            this.Logger = resolver.GetLogger<EfDataProcessor>();
        }

        public void Process(WorkflowInstance inst)
        {
            List<FormData> inserted = new List<FormData>();
            var dataContext = (EfDataContext)inst.Data;

            InitServiceContext(dataContext);

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

                if (removed.Any()) { FormDataService.Delete(removed.Select(x => x.Id), scope.SessionHandle); }
                modified.ForEach(x => { FormDataService.Replace(x, scope.SessionHandle); });
                if (inserted.Any())
                {
                    FormDataService.Add(inserted, scope.SessionHandle);
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
                        var data = new WfDataContext(x.CorpId ?? "", dataContext.UserId, dataContext.AccessToken, x.AppId, x.FormId, x.Id, x.CreateBy, dataContext.EfCascade, dataContext.EventIds);
                        WorkflowHost.StartWorkflow(x.FormId, data.ToExpando());
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "EventFlow自动提交表单失败。FormDataId={FormDataId}", x.Id);
                }
            });
        }

        private void InitServiceContext(EfDataContext dataContext)
        {
            ServiceContext.CorpId = dataContext.CorpId;
            ServiceContext.UserId = dataContext.UserId;
            ServiceContext.AccessToken = dataContext.AccessToken;
            ServiceContext.Operator = dataContext.WfStarter ?? Operator.Empty;
            ServiceContext.Action = DataAction.EventFlow;
        }
    }
}
