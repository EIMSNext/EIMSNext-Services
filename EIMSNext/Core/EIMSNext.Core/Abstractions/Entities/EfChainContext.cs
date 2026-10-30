namespace EIMSNext.Core.Abstractions
{
    /// <summary>
    /// EventFlow 链上下文：沿触发链显式传播，运行期循环/深度校验的载体。
    /// 无上下文 = 新链（用户操作/定时/外部回调）。
    /// </summary>
    public class EfChainContext
    {
        /// <summary>链标识：同一条触发链全程不变。</summary>
        public string ChainId { get; set; } = string.Empty;

        /// <summary>当前深度：链上已执行的 EventFlow 层数。</summary>
        public int Depth { get; set; }

        /// <summary>已执行路径：链上执行过的 EventFlowId，用于环检测。</summary>
        public List<string> Path { get; set; } = new();

        /// <summary>
        /// 校验跨边界传入的链上下文。无效上下文必须按新链处理，不能让外部输入绕过深度限制
        /// 或以 null Path 触发运行时异常。
        /// </summary>
        public bool IsValid(int maxDepth)
        {
            if (maxDepth <= 0
                || string.IsNullOrWhiteSpace(ChainId)
                || Depth < 1
                || Depth > maxDepth
                || Path is null
                || Path.Count != Depth
                || Path.Count > maxDepth)
            {
                return false;
            }

            return Path.All(id => !string.IsNullOrWhiteSpace(id) && id.Length <= 200);
        }
    }

    /// <summary>
    /// EventFlow 链校验常量。
    /// </summary>
    public static class EfChainConsts
    {
        /// <summary>链深度上限：超过即中断整链（防工作流事件链无限步进）。</summary>
        public const int MaxChainDepth = 10;

        /// <summary>深度上限配置键（Flow.Host appsettings），缺省时取 <see cref="MaxChainDepth"/>。</summary>
        public const string MaxChainDepthConfigKey = "EventFlow:MaxChainDepth";

        /// <summary>解析深度上限：非法/缺省配置回落到 <see cref="MaxChainDepth"/>。键规则唯一入口，勿在调用方复制。</summary>
        public static int ResolveMaxChainDepth(string? raw)
        {
            return int.TryParse(raw, out var maxDepth) && maxDepth > 0
                ? maxDepth
                : MaxChainDepth;
        }
    }
}
