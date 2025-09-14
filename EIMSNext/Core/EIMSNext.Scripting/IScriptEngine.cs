namespace EIMSNext.Scripting
{
    public interface IScriptEngine : IDisposable
    {
        EvaluationResult<dynamic> Evaluate(string script, IDictionary<string, object>? parameters = null);
        EvaluationResult<T> Evaluate<T>(string script, IDictionary<string, object>? parameters = null);
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
    }
}
