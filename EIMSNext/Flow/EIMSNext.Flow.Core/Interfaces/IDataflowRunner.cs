using EIMSNext.Core.Entities;
using EIMSNext.Service.Entities;

using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Interfaces
{
    public interface IDataflowRunner
    {
        Task<DfExecResult> RunAsync(DfRunParamter paramter);
        bool IsMeet(Wf_Definition dataflow, FormData data);
    }

    public class DfRunParamter
    {
        public DfRunParamter(string userId, string accessToken, FormData data, EventSourceType eventSource, EventType eventType, string wfNodeId, Operator? starter, CascadeMode cascade, string? eventIds)
        {
            UserId = userId;
            AccessToken = accessToken;
            Data = data;
            EventSource = eventSource;
            EventType = eventType;
            WfNodeId = wfNodeId;
            Starter = starter;
            Cascade = cascade;
            EventIds = eventIds;
        }

        public DfRunParamter WithDataflowId(string dataflowId)
        {
            DataflowId = dataflowId;
            return this;
        }

        public DfRunParamter WithNodeAction(string? nodeAction)
        {
            NodeAction = nodeAction;
            return this;
        }

        public DfRunParamter WithChangeFields(IEnumerable<string>? changeFields)
        {
            ChangeFields = changeFields?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return this;
        }

        public string UserId { get; private set; }
        public string AccessToken { get; private set; }
        public FormData Data { get; private set; }
        public string DataflowId { get; private set; } = string.Empty;
        public EventSourceType EventSource { get; private set; }
        public EventType EventType { get; private set; }
        public string WfNodeId { get; private set; }
        public string? NodeAction { get; private set; }
        public Operator? Starter { get; private set; }
        public CascadeMode Cascade { get; private set; }
        public string? EventIds { get; private set; }
        public IReadOnlyCollection<string>? ChangeFields { get; private set; }
    }

    public class DfExecResult
    {
        public bool Success => string.IsNullOrEmpty(Error);
        public WorkflowInstance? DfInstance { get; set; }
        public string? Error { get; set; }
    }
}
