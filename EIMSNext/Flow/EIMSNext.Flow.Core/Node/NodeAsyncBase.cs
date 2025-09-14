using HKH.Mef2.Integration;

using EIMSNext.Entity;

using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Node
{
    public abstract class NodeAsyncBase : StepBodyAsync, IFlowNode
    {
        protected IResolver Resolver { get; private set; }
        public WfStep? Metadata { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="resolver"></param>
        protected NodeAsyncBase(IResolver resolver)
        {
            this.Resolver = resolver;
        }
    }
}
