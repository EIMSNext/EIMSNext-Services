using HKH.Mef2.Integration;

using EIMSNext.Service.Entities;

using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Nodes
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
