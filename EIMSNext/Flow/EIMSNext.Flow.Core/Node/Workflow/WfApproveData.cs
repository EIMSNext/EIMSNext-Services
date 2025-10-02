using System.Dynamic;
using EIMSNext.Entity;

namespace EIMSNext.Flow.Core
{
    public class WfApproveData
    {
        private WfApproveData() { }
        public WfApproveData(string corpId, string userId, string workerId, string workerName, ApproveAction action, string comment, string signature, string execLogId)
        {
            CorpId = corpId;
            UserId = userId;
            WorkerId = workerId;
            WorkerName = workerName;
            Action = action;
            Comment = comment;
            Signature = signature;
            ExecLogId = execLogId;
        }

        public static WfApproveData FromExpando(ExpandoObject expando)
        {
            var ctx = new WfApproveData();
            ctx.CorpId = expando.GetValue(WfConsts.WorkerCorpId, string.Empty);
            ctx.UserId = expando.GetValue(WfConsts.WorkerUserId, string.Empty);
            ctx.WorkerId = expando.GetValue(WfConsts.WorkerId, string.Empty);
            ctx.WorkerName = expando.GetValue(WfConsts.WorkerName, string.Empty);
            ctx.Action = expando.GetValue(WfConsts.ApproveAction, ApproveAction.None);
            ctx.Comment = expando.GetValue(WfConsts.ApproveComment, string.Empty);
            ctx.Signature = expando.GetValue(WfConsts.ApproveSignature, string.Empty);
            ctx.ExecLogId = expando.GetValue(WfConsts.ApproveLogId, string.Empty);

            return ctx;
        }

        public string CorpId { get; private set; } = string.Empty;
        public string UserId { get; private set; } = string.Empty;
        public string WorkerId { get; private set; } = string.Empty;
        public string WorkerName { get; private set; } = string.Empty;
        public ApproveAction Action { get; private set; }
        public string Comment { get; private set; } = string.Empty;
        public string Signature { get; private set; } = string.Empty;
        public string ExecLogId { get; private set; } = string.Empty;

        public ExpandoObject ToExpando()
        {
            var approveData = new ExpandoObject();

            approveData.AddOrUpdate(WfConsts.WorkerCorpId, CorpId);
            approveData.AddOrUpdate(WfConsts.WorkerUserId, UserId);
            approveData.AddOrUpdate(WfConsts.WorkerId, WorkerId);
            approveData.AddOrUpdate(WfConsts.WorkerName, WorkerName);
            approveData.AddOrUpdate(WfConsts.ApproveAction, Action);
            approveData.AddOrUpdate(WfConsts.ApproveComment, Comment);
            approveData.AddOrUpdate(WfConsts.ApproveSignature, Signature);
            approveData.AddOrUpdate(WfConsts.ApproveLogId, ExecLogId);

            return approveData;
        }
    }
}
