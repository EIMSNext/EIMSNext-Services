using EIMSNext.Core.Entity;
using EIMSNext.Entity;

using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Interface
{
    public interface IDataflowRunner
    {
        Task<DfExecResult> RunAsync(FormData data, EventSourceType eventSource, EventType eventType, string wfNodeId, Operator? starter, CascadeMode cascade, string? eventIds);
        bool IsMeet(Wf_Definition dataflow, FormData data);
    }

    public class DfExecResult
    {
        public bool Success => string.IsNullOrEmpty(Error);

        public WorkflowInstance? DfInstance { get; set; }
        public string? Error { get; set; }
    }
}
