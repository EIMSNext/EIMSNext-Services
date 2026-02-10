using EIMSNext.Core.Entity;
using EIMSNext.Entity;

using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Interface
{
    public interface IDataflowRunner
    {
        Task<DfExecResult> RunAsync(DfRunParamter paramter);
        bool IsMeet(Wf_Definition dataflow, FormData data);
    }

    public class DfRunParamter
    {
        public DfRunParamter(string userId, FormData data, EventSourceType eventSource, EventType eventType, string wfNodeId, Operator? starter, CascadeMode cascade, string? eventIds)
        {
            UserId = userId;
            Data = data;
            EventSource = eventSource;
            EventType = eventType;
            WfNodeId = wfNodeId;
            Starter = starter;
            Cascade = cascade;
            EventIds = eventIds;
        }

        public string UserId { get; private set; }
        public FormData Data { get; private set; }
        public EventSourceType EventSource { get; private set; }
        public EventType EventType { get; private set; }
        public string WfNodeId { get; private set; }
        public Operator? Starter { get; private set; }
        public CascadeMode Cascade { get; private set; }
        public string? EventIds { get; private set; }
    }

    public class DfExecResult
    {
        public bool Success => string.IsNullOrEmpty(Error);
        public WorkflowInstance? DfInstance { get; set; }
        public string? Error { get; set; }
    }
}
