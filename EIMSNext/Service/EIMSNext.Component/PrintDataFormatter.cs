using System.Collections;
using System.Dynamic;
using System.Globalization;
using System.Text.Json;
using EIMSNext.Core.Abstractions;
using EIMSNext.Common.Extensions;
using EIMSNext.Entities;

namespace EIMSNext.Component
{
    public sealed class PrintDataContext
    {
        public string CurrentNode { get; init; } = string.Empty;
        public string CurrentOwner { get; init; } = string.Empty;
        public string InternalDataUrl { get; init; } = string.Empty;
        public string ExternalDataUrl { get; init; } = string.Empty;
        public Operator? PrintedBy { get; init; }
        public long? PrintedTime { get; init; }
    }

    /// <summary>
    /// Builds the data contract consumed by custom print templates.
    /// </summary>
    public static class PrintDataFormatter
    {
        public const string ApprovalLogs = "approvallogs";

        public static ExpandoObject Format(
            FormData data,
            IList<FieldDef> fieldDefs,
            IEnumerable<Wf_TaskLog>? taskLogs = null,
            PrintDataContext? context = null)
        {
            var result = FormDataFormatter.Format(data, fieldDefs);
            var values = (IDictionary<string, object?>)result;
            context ??= new PrintDataContext();

            values["createBy"] = data.CreateBy?.Label ?? string.Empty;
            values["createTime"] = FormatTimestamp(data.CreateTime);
            values["updateTime"] = data.UpdateTime.HasValue ? FormatTimestamp(data.UpdateTime.Value) : string.Empty;
            values["ext"] = GetFormDataValue(data, "ext");
            values["flowStatus"] = FormatFlowStatus(data.FlowStatus);
            values["currentNode"] = context.CurrentNode;
            values["currentOwner"] = context.CurrentOwner;
            values["internalQrCode"] = context.InternalDataUrl;
            values["externalQrCode"] = context.ExternalDataUrl;
            values["printedBy"] = context.PrintedBy?.Label ?? string.Empty;
            values["printedTime"] = FormatTimestamp(context.PrintedTime ?? DateTime.UtcNow.ToTimeStampMs());
            values[ApprovalLogs] = BuildApprovalLogs(taskLogs ?? []);

            return result;
        }

        private static List<ExpandoObject> BuildApprovalLogs(IEnumerable<Wf_TaskLog> taskLogs)
        {
            var latestRoundByNode = taskLogs
                .Where(x => x.NodeType == WfNodeType.Approve)
                .GroupBy(x => x.NodeId)
                .ToDictionary(x => x.Key, x => x.Max(log => log.Round));

            return taskLogs
                .Where(x => x.NodeType == WfNodeType.Approve
                    && latestRoundByNode.TryGetValue(x.NodeId, out var round)
                    && x.Round == round)
                .OrderBy(x => x.ApprovalTime)
                .ThenBy(x => x.NodeId, StringComparer.Ordinal)
                .Select((log, index) => ToApprovalLog(log, index + 1))
                .ToList();
        }

        private static ExpandoObject ToApprovalLog(Wf_TaskLog log, int sequence)
        {
            var item = new ExpandoObject();
            var values = (IDictionary<string, object?>)item;
            values["sequence"] = sequence;
            values["approvalTime"] = FormatTimestamp(log.ApprovalTime);
            values["nodeName"] = log.NodeName;
            values["approver"] = log.Approver?.Label ?? string.Empty;
            values["comment"] = log.Comment ?? string.Empty;
            values["result"] = FormatApproveAction(log.Result);
            return item;
        }

        private static string GetFormDataValue(FormData data, string field)
        {
            if (data.Data is not IDictionary<string, object?> values)
            {
                return string.Empty;
            }

            var value = values.FirstOrDefault(x => string.Equals(x.Key, field, StringComparison.OrdinalIgnoreCase)).Value;
            return ToDisplayString(value);
        }

        private static string ToDisplayString(object? value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (value is JsonElement jsonElement)
            {
                return jsonElement.ValueKind == JsonValueKind.String
                    ? jsonElement.GetString() ?? string.Empty
                    : jsonElement.ToString();
            }

            return value.ToString() ?? string.Empty;
        }

        private static string FormatTimestamp(long timestamp)
        {
            if (timestamp <= 0)
            {
                return string.Empty;
            }

            var localTime = TimeZoneInfo.ConvertTimeFromUtc(timestamp.ToDateTimeMs(), GetShanghaiTimeZone());
            return localTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        private static TimeZoneInfo GetShanghaiTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
            }
        }

        private static string FormatFlowStatus(FlowStatus status) => status switch
        {
            FlowStatus.Draft => "草稿",
            FlowStatus.Approving => "审批中",
            FlowStatus.Approved => "已审批",
            FlowStatus.Rejected => "已驳回",
            FlowStatus.Suspended => "已挂起",
            FlowStatus.Discarded => "已废弃",
            _ => string.Empty,
        };

        private static string FormatApproveAction(ApproveAction action) => action switch
        {
            ApproveAction.Approve => "通过",
            ApproveAction.Reject => "驳回",
            ApproveAction.Return => "退回",
            ApproveAction.AddSignPre => "前加签",
            ApproveAction.AddSignAfter => "后加签",
            ApproveAction.AutoApprove => "自动通过",
            ApproveAction.Withdraw => "撤回",
            ApproveAction.Transfer => "转交",
            ApproveAction.AutoReject => "自动驳回",
            ApproveAction.AutoReturn => "自动退回",
            ApproveAction.AutoTransfer => "自动转交",
            ApproveAction.ChangeApprover => "变更审批人",
            _ => string.Empty,
        };
    }
}
