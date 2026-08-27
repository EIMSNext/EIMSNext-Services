using EIMSNext.Entities;

namespace EIMSNext.Flow.Core.Nodes
{
    public interface IFlowNode
    {
        public WfStep? Metadata { get; set; }
    }
}
