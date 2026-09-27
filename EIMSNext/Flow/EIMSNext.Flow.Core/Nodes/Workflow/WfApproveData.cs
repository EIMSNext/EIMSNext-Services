using System.Dynamic;

using EIMSNext.Entities;
using EIMSNext.Core.Extensions;

namespace EIMSNext.Flow.Core
{
    public class WfApproveData
    {
        private WfApproveData() { }
        public WfApproveData(string corpId, string userId, string workerId, string workerCode, string workerName, ApproveAction action, string comment, string signature, string execLogId)
        {
            CorpId = corpId;
            UserId = userId;
            WorkerId = workerId;
            WorkerCode = workerCode;
            WorkerName = workerName;
            Action = action;
            Comment = comment;
            Signature = signature;
            ExecLogId = execLogId;
        }

        public static WfApproveData FromData(IDictionary<string, object?> data)
        {
            var ctx = new WfApproveData();
            ctx.CorpId = data.GetValue(WfConsts.WorkerCorpId, string.Empty);
            ctx.UserId = data.GetValue(WfConsts.WorkerUserId, string.Empty);
            ctx.WorkerId = data.GetValue(WfConsts.WorkerId, string.Empty);
            ctx.WorkerCode = data.GetValue(WfConsts.WorkerCode, string.Empty);
            ctx.WorkerName = data.GetValue(WfConsts.WorkerName, string.Empty);
            ctx.Action = data.GetValue(WfConsts.ApproveAction, ApproveAction.None);
            ctx.Comment = data.GetValue(WfConsts.ApproveComment, string.Empty);
            ctx.Signature = data.GetValue(WfConsts.ApproveSignature, string.Empty);
            ctx.ExecLogId = data.GetValue(WfConsts.ApproveLogId, string.Empty);

            return ctx;
        }

        public string CorpId { get; private set; } = string.Empty;
        public string UserId { get; private set; } = string.Empty;
        public string WorkerId { get; private set; } = string.Empty;
        public string WorkerCode { get; private set; } = string.Empty;
        public string WorkerName { get; private set; } = string.Empty;
        public ApproveAction Action { get; private set; }
        public string Comment { get; private set; } = string.Empty;
        public string Signature { get; private set; } = string.Empty;
        public string ExecLogId { get; private set; } = string.Empty;

        // 写入口保持 ExpandoObject：经 SubmitActivitySuccess 进入 WorkflowCore 的
        // ExecutionPointer.EventData（ActivityResult.Data），由持久化层 $type 契约还原。
        public ExpandoObject ToExpando()
        {
            var approveData = new ExpandoObject();

            approveData.AddOrUpdate(WfConsts.WorkerCorpId, CorpId);
            approveData.AddOrUpdate(WfConsts.WorkerUserId, UserId);
            approveData.AddOrUpdate(WfConsts.WorkerId, WorkerId);
            approveData.AddOrUpdate(WfConsts.WorkerCode, WorkerCode);
            approveData.AddOrUpdate(WfConsts.WorkerName, WorkerName);
            approveData.AddOrUpdate(WfConsts.ApproveAction, Action);
            approveData.AddOrUpdate(WfConsts.ApproveComment, Comment);
            approveData.AddOrUpdate(WfConsts.ApproveSignature, Signature);
            approveData.AddOrUpdate(WfConsts.ApproveLogId, ExecLogId);

            return approveData;
        }
    }
}
