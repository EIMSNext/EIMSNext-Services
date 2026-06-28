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

    public class ScriptEngineOption
    {
        public int MinPoolSize { get; set; } = 5;
        public int MaxPoolSize { get; set; } = 100;
        public TimeSpan MaxIdleTime { get; set; } = TimeSpan.FromMinutes(3);
        public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// 单次 <see cref="IScriptEngine.Evaluate"/> 调用的默认超时。
        /// 调用方未显式传入 <see cref="CancellationToken"/> 时,引擎内部以此值为上限
        /// 自动创建 <see cref="CancellationTokenSource"/>。
        /// 设为 <see cref="TimeSpan.Zero"/> 表示禁用默认超时(此时只有显式 CT 才生效)。
        /// </summary>
        public TimeSpan DefaultEvaluationTimeout { get; set; } = TimeSpan.FromSeconds(5);
    }
}
