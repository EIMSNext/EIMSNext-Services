namespace EIMSNext.Core.Entity
{
    public interface IServiceContext
    {
        public string AccessToken { get; set; }
        public string CorpId { get; set; }
        public Operator? Operator { get; set; }
        public IUser? User { get; set; }
        public IEmployee? Employee { get; set; }

        public DataAction Action { get; set; }
    }
    public sealed class Operator
    {
        public static Operator _empty = new Operator("", "", "", "");

        public Operator()
        { }
        public Operator(string? corpId, string userId, string empId, string empName)
        {
            CorpId = corpId;
            UserId = userId;
            EmpId = empId;
            EmpName = empName;
        }

        public string? CorpId { get; set; }
        public string? UserId { get; set; }
        public string EmpId { get; set; } = string.Empty;
        public string EmpName { get; set; } = string.Empty;

        public static Operator Empty => _empty;
    }

    public enum DataAction
    {
        None,
        /// <summary>
        /// 保存草稿
        /// </summary>
        SaveDraft,
        /// <summary>
        /// 流程提交
        /// </summary>
        Submit,
        /// <summary>
        /// 流程审批
        /// </summary>
        Approve,
        /// <summary>
        /// 流程退回
        /// </summary>
        Return
    }
}
