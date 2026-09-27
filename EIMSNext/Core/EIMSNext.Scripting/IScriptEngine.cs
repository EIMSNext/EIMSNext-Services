namespace EIMSNext.Scripting
{
    public interface IScriptEngine : IDisposable
    {
        EvaluationResult<dynamic> Evaluate(string script, IDictionary<string, object>? parameters = null, CancellationToken ct = default);
        EvaluationResult<T> Evaluate<T>(string script, IDictionary<string, object>? parameters = null, CancellationToken ct = default);
    }

    public class EvaluationResult<T>
    {
        public T? Value { get; set; }
        public string? Error { get; set; }
        public bool Success => string.IsNullOrEmpty(Error);
    }

    /// <summary>
    /// 脚本引擎堆超限行为。引擎实现无关的抽象。
    /// </summary>
    public enum ScriptViolationPolicy
    {
        /// <summary>
        /// 抛 <c>ScriptEngineException</c> 等托管异常,由 <see cref="IScriptEngine.Evaluate"/> 转 <c>EvaluationResult.Error</c>。
        /// 生产环境推荐:可观测、可恢复、不杀进程。
        /// </summary>
        Exception = 0,

        /// <summary>
        /// 尽力打断脚本。堆可能继续涨。仅在需要"优雅降级"场景使用。
        /// </summary>
        Interrupt = 1,
    }

    public class ScriptEngineOption
    {
        public int MinPoolSize { get; set; } = 1;
        public int MaxPoolSize { get; set; } = 5;
        public TimeSpan MaxIdleTime { get; set; } = TimeSpan.FromMinutes(3);
        public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// 单次 <see cref="IScriptEngine.Evaluate"/> 调用的默认超时。
        /// 调用方未显式传入 <see cref="CancellationToken"/> 时,引擎内部以此值为上限
        /// 自动创建 <see cref="CancellationTokenSource"/>。
        /// 设为 <see cref="TimeSpan.Zero"/> 表示禁用默认超时(此时只有显式 CT 才生效)。
        /// </summary>
        public TimeSpan DefaultEvaluationTimeout { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 单个 runtime 新生代堆上限(MiB)。0 表示用引擎默认(通常 16 MiB)。
        /// </summary>
        public int MaxNewSpaceSizeMB { get; set; } = 16;

        /// <summary>
        /// 单个 runtime 堆硬上限(MiB)。触达后由引擎决定行为(通常杀进程)。
        /// 0 表示不限制(7.4.5 行为,生产环境强烈建议 >= 64)。
        /// 应大于 <see cref="MaxRuntimeHeapSizeMB"/>,作为最后安全网。
        /// </summary>
        public int MaxOldSpaceSizeMB { get; set; } = 256;

        /// <summary>
        /// 单个 runtime 堆软上限(MiB)。引擎周期性采样,超过时按 <see cref="ViolationPolicy"/> 抛异常或打断。
        /// 必须显著小于 <see cref="MaxOldSpaceSizeMB"/>(引擎实现通常建议 70-80%)。
        /// 0 禁用软监控(此时触达硬上限即进程被引擎杀掉)。
        /// </summary>
        public int MaxRuntimeHeapSizeMB { get; set; } = 192;

        /// <summary>
        /// 单次 ArrayBuffer 分配上限(字节)。ArrayBuffer 内存分配在 V8 堆外,需独立约束。
        /// 0 表示不限制。
        /// </summary>
        public long MaxArrayBufferAllocation { get; set; } = 64L * 1024 * 1024;

        /// <summary>
        /// 堆超限行为。<see cref="ScriptViolationPolicy.Exception"/> 推荐生产:
        /// 抛 <c>ScriptEngineException</c> 等托管异常,该 engine 标记 IsBroken 重建;
        /// <see cref="ScriptViolationPolicy.Interrupt"/> 仅尝试打断,堆可能继续涨。
        /// </summary>
        public ScriptViolationPolicy ViolationPolicy { get; set; } = ScriptViolationPolicy.Exception;
    }
}
