using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;

namespace EIMSNext.Service.Host.Models
{
    public class FlowManageTaskItem
    {
        public string TaskId { get; set; } = string.Empty;
        public string WfInstanceId { get; set; } = string.Empty;
        public string DataId { get; set; } = string.Empty;
        public string FormName { get; set; } = string.Empty;
        public Operator? Starter { get; set; }
        public string CurrentApproverName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string ApproveNodeId { get; set; } = string.Empty;
        public string ApproveNodeName { get; set; } = string.Empty;
        public long ApproveNodeStartTime { get; set; }
    }

    public class FlowManageTaskQueryResult
    {
        public List<FlowManageTaskItem> Items { get; set; } = [];
        public long Total { get; set; }
    }
}
