using EIMSNext.Cache;

namespace EIMSNext.Core.Abstractions
{
    /// <summary>
    /// 服务上下文接口，提供当前请求的访问令牌、企业与操作者等信息。
    /// </summary>
    public interface IServiceContext
    {
        /// <summary>
        /// 获取或设置访问令牌。
        /// </summary>
        public string AccessToken { get; set; }

        /// <summary>
        /// 获取或设置企业 ID。
        /// </summary>
        public string CorpId { get; set; }

        /// <summary>
        /// 获取或设置当前操作者。
        /// </summary>
        public Operator? Operator { get; set; }

        /// <summary>
        /// 获取或设置用户 ID。
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// 获取或设置当前用户。
        /// </summary>
        public IUser? User { get; set; }

        /// <summary>
        /// 获取或设置当前员工。
        /// </summary>
        public IEmployee? Employee { get; set; }

        /// <summary>
        /// 获取或设置客户端 IP。
        /// </summary>
        public string? ClientIp { get; set; }

        /// <summary>
        /// 获取或设置数据操作类型。
        /// </summary>
        public DataAction Action { get; set; }

        /// <summary>
        /// 获取作用域缓存。
        /// </summary>
        IScopeCache ScopeCache { get; }
    }

    /// <summary>
    /// 操作者，表示当前执行操作的用户或员工。
    /// </summary>
    public sealed class Operator
    {
        /// <summary>
        /// 空操作者实例。
        /// </summary>
        public static Operator _empty = new Operator("", "", "");

        /// <summary>
        /// 初始化 <see cref="Operator"/> 类的新实例。
        /// </summary>
        public Operator()
        { }

        /// <summary>
        /// 使用指定标识、编码与名称初始化 <see cref="Operator"/> 类的新实例。
        /// </summary>
        /// <param name="id">员工 ID。</param>
        /// <param name="value">员工编码。</param>
        /// <param name="label">员工姓名。</param>
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

        /// <summary>
        /// 获取空操作者实例。
        /// </summary>
        public static Operator Empty => _empty;
    }

    /// <summary>
    /// 数据操作类型。
    /// </summary>
    public enum DataAction
    {
        /// <summary>
        /// 无操作。
        /// </summary>
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
        Return,
        /// <summary>
        /// 数据流内部写入
        /// </summary>
        EventFlow
    }
}
