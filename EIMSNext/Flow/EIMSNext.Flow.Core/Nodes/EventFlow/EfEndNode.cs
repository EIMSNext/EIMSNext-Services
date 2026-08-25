using HKH.Mef2.Integration;

using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Flow.Core.Interfaces;

using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Nodes
{
    public class EfEndNode : EfNodeBase<EfEndNode>
    {
        public EfEndNode(IResolver resolver) : base(resolver)
        {
            _dataProcessor = resolver.Resolve<IEfDataProcessor>();
        }

        private IEfDataProcessor _dataProcessor;

        public override ExecutionResult Run(IStepExecutionContext context)
        {
            return ExecuteWithLog(context, dataContext =>
            {
                _dataProcessor.Process(context.Workflow);
                return ExecutionResult.Next();
            });
        }
    }
}
