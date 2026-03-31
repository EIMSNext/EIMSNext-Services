using HKH.Mef2.Integration;

using EIMSNext.Service.Entities;

using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Nodes
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
