using EIMSNext.Entity;

namespace EIMSNext.Flow.Core.Node
{
    public interface IFlowNode
    {
        public WfStep? Metadata { get; set; }
    }
}
