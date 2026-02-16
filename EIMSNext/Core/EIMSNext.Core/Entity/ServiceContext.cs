using EIMSNext.Cache;

namespace EIMSNext.Core.Entity
{
    public interface IServiceContext
    {
        public string AccessToken { get; set; }
        public string CorpId { get; set; }
        public Operator? Operator { get; set; }
        public string UserId { get; set; }
        public IUser? User { get; set; }
        public IEmployee? Employee { get; set; }
        public string? ClientIp { get; set; }
        public DataAction Action { get; set; }
        ISessionStore SessionStore { get; }
    }

    public sealed class Operator
    {
        public static Operator _empty = new Operator("", "", "");

        public Operator()
        { }
        public Operator(string id, string value, string label)
        {
            Id = id;
            Label = label;
            Value = value;
        }

        /// <summary>
        /// 员工Id
        /// </summary>
        public string Id { get; set; } = string.Empty;
        /// <summary>
        /// 员工编码
        /// </summary>
        public string Value { get; set; } = string.Empty;
        /// <summary>
        /// 员工姓名
        /// </summary>
        public string Label { get; set; } = string.Empty;

        public static Operator Empty => _empty;
    }

    public enum DataAction
    {
        None,
        /// <summary>
        /// 保存草稿
        /// </summary>
        Save,
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
