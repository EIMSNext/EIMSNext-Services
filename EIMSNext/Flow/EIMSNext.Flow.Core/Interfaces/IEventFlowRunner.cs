using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Entities;

using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Interfaces
{
    public interface IEventFlowRunner
    {
        Task<EfExecResult> RunAsync(EfRunParameter paramter);
        bool IsMeet(Wf_Definition eventFlow, FormData data);
    }

    public class EfRunParameter
    {
        public EfRunParameter(string userId, string accessToken, FormData data, EventSourceType eventSource, EventType eventType, string wfNodeId, Operator? starter, CascadeMode cascade, string? eventIds)
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

        public EfRunParameter WithEventFlowId(string eventFlowId)
        {
            EventFlowId = eventFlowId;
            return this;
        }

        public EfRunParameter WithNodeAction(string? nodeAction)
        {
            NodeAction = nodeAction;
            return this;
        }

        public EfRunParameter WithChangeFields(IEnumerable<string>? changeFields)
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
        public string EventFlowId { get; private set; } = string.Empty;
        public EventSourceType EventSource { get; private set; }
        public EventType EventType { get; private set; }
        public string WfNodeId { get; private set; }
        public string? NodeAction { get; private set; }
        public Operator? Starter { get; private set; }
        public CascadeMode Cascade { get; private set; }
        public string? EventIds { get; private set; }
        public IReadOnlyCollection<string>? ChangeFields { get; private set; }
    }

    public class EfExecResult
    {
        public bool Success => string.IsNullOrEmpty(Error);
        public WorkflowInstance? EfInstance { get; set; }
        public string? Error { get; set; }
    }
}
