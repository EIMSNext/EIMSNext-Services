using HKH.Mef2.Integration;

using EIMSNext.Entity;

using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Node
{
    public abstract class NodeBase : StepBody, IFlowNode
    {
        protected IResolver Resolver { get; private set; }

        public WfStep? Metadata { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="resolver"></param>
        protected NodeBase(IResolver resolver)
        {
            this.Resolver = resolver;
        }
    }
}
