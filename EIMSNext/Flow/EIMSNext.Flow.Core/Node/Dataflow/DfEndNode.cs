using HKH.Mef2.Integration;

using EIMSNext.Core;
using EIMSNext.Flow.Core.Interface;

using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Node
{
    public class DfEndNode : DfNodeBase<DfEndNode>
    {
        public DfEndNode(IResolver resolver) : base(resolver)
        {
            _dataProcessor = resolver.Resolve<IDfDataProcessor>();
        }

        private IDfDataProcessor _dataProcessor;

        public override ExecutionResult Run(IStepExecutionContext context)
        {
            _dataProcessor.Process(context.Workflow);

            return ExecutionResult.Next();
        }
    }
}
