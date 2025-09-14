
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;

using NLog;

namespace EIMSNext.Scripting
{
    public class V8ScriptEngine : IScriptEngine
    {

        #region 配置参数
        private ScriptEngineOption _option;
        private int MinPoolSize => _option.MinPoolSize;
        private int MaxPoolSize => _option.MaxPoolSize;
        private TimeSpan MaxIdleTime => _option.MaxIdleTime;
        private TimeSpan CleanupInterval => _option.CleanupInterval;
        #endregion

        #region 引擎管理
        private class PooledEngine
        {
            public Microsoft.ClearScript.V8.V8ScriptEngine Engine { get; }
            public DateTime LastUsedTime { get; set; }
            public bool IsBroken { get; set; }

            public PooledEngine(Microsoft.ClearScript.V8.V8ScriptEngine engine)
            {
                Engine = engine;
                LastUsedTime = DateTime.UtcNow;
            }
        }

        private readonly ConcurrentBag<PooledEngine> _idleEngines = new();
        private readonly ConcurrentDictionary<Microsoft.ClearScript.V8.V8ScriptEngine, bool> _activeEngines = new();
        private int _totalEngines;
        private readonly Timer _cleanupTimer;
        private List<string>? _jsFiles;

        #endregion

        public V8ScriptEngine(ScriptEngineOption option)
        {
            _option = option;
            InitializePool();
            _cleanupTimer = new Timer(CleanupCallback, null, CleanupInterval, CleanupInterval);
        }

        private void InitializePool()
        {
            Parallel.For(0, MinPoolSize, _ =>
            {
                var engine = CreateEngine();
                _idleEngines.Add(new PooledEngine(engine));
                Interlocked.Increment(ref _totalEngines);
            });
        }

        private Microsoft.ClearScript.V8.V8ScriptEngine CreateEngine()
        {
            var engine = new Microsoft.ClearScript.V8.V8ScriptEngine(V8ScriptEngineFlags.EnableDynamicModuleImports);

            // 预加载公共函数库
            var jsFiles = LoadJsFiles();
            if (jsFiles.Count > 0)
            {
                jsFiles.ForEach(jsFile =>
                {
                    engine.Execute(jsFile);
                });
            }

            return engine;
        }

        private List<string> LoadJsFiles()
        {
            if (_jsFiles?.Count > 0)
                return _jsFiles;

            var files = new List<string>();

            lock ("FormulaJs")
            {
                if (_jsFiles == null)
                {
                    var assembly = Assembly.GetExecutingAssembly();
                    var formulaStream = assembly.GetManifestResourceStream("EIMSNext.Scripting.formula.js");

                    if (formulaStream != null)
                    {
                        using (StreamReader reader = new StreamReader(formulaStream))
                        {
                            var formulaJs = reader.ReadToEnd();
                            if (!string.IsNullOrEmpty(formulaJs))
                                files.Add(formulaJs);
                        }
                    }

                    var filterStream = assembly.GetManifestResourceStream("EIMSNext.Scripting.filter.js");

                    if (filterStream != null)
                    {
                        using (StreamReader reader = new StreamReader(filterStream))
                        {
                            var filterJs = reader.ReadToEnd();
                            if (!string.IsNullOrEmpty(filterJs))
                                files.Add(filterJs);
                        }
                    }
                }
            }

            if (files.Count > 0)
                _jsFiles = files;

            return files;
        }

        public EvaluationResult<dynamic> Evaluate(string script, IDictionary<string, object>? parameters = null)
        {
            var result = new EvaluationResult<dynamic>();
            if (string.IsNullOrEmpty(script) || script == ScriptExpression.TRUE)
            {
                result.Value = true;
                return result;
            }
            if (script == ScriptExpression.FALSE)
            {
                result.Value = false;
                return result;
            }

            var engine = RentEngine();
            try
            {
                // 注入参数
                if (parameters != null)
                {
                    foreach (var param in parameters)
                    {
                        if (param.Key == "data")
                            engine.Engine.AddHostObject(param.Key, param.Value);
                        else
                            engine.Engine.Script[param.Key] = param.Value;
                    }
                }

                // 执行脚本
                result.Value = engine.Engine.Evaluate($"(() => {{ return {script} }})()");
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                LogManager.GetCurrentClassLogger().Error(ex, $"JS执行错误: {script}, {JsonSerializer.Serialize(parameters)}");
                if (!TestEngine(engine)) // Test V8Engine
                    engine.IsBroken = true;
            }
            finally
            {
                // 清理参数
                if (parameters != null)
                {
                    foreach (var key in parameters.Keys)
                    {
                        engine.Engine.Script[key] = Undefined.Value;
                    }
                }

                ReturnEngine(engine);
            }

            return result;
        }
        public EvaluationResult<T> Evaluate<T>(string script, IDictionary<string, object>? parameters = null)
        {
            var result = new EvaluationResult<T>();
            try
            {
                var temp = Evaluate(script, parameters);
                result.Error = temp.Error;
                if (string.IsNullOrEmpty(result.Error))
                    result.Value = (T)Convert.ChangeType(temp.Value, typeof(T));
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                result.Value = default(T);
            }
            return result;
        }
        private PooledEngine RentEngine()
        {
            if (_idleEngines.TryTake(out var engine))
            {
                _activeEngines.TryAdd(engine.Engine, true);
                engine.LastUsedTime = DateTime.UtcNow;
                return engine;
            }

            if (_totalEngines < MaxPoolSize)
            {
                var newEngine = new PooledEngine(CreateEngine());
                Interlocked.Increment(ref _totalEngines);
                _activeEngines.TryAdd(newEngine.Engine, true);
                return newEngine;
            }

            // 等待可用引擎（带超时）
            var waitStart = DateTime.UtcNow;
            while (DateTime.UtcNow - waitStart < TimeSpan.FromSeconds(30))
            {
                if (_idleEngines.TryTake(out engine))
                {
                    _activeEngines.TryAdd(engine.Engine, true);
                    return engine;
                }
                Thread.Sleep(50);
            }

            throw new TimeoutException("No available V8 engine after 30 seconds");
        }
        private bool TestEngine(PooledEngine engine)
        {
            try
            {
                return Convert.ToInt32(engine.Engine.Evaluate($"(() => {{ return 1+1 }})()")) == 2;
            }
            catch
            {
                return false;
            }
        }
        private void ReturnEngine(PooledEngine engine)
        {
            if (engine == null) return;

            _activeEngines.TryRemove(engine.Engine, out _);
            if (!engine.IsBroken)
            {
                _idleEngines.Add(engine);
                engine.LastUsedTime = DateTime.UtcNow;
            }
            else
            {
                engine.Engine.Dispose();
                Interlocked.Decrement(ref _totalEngines);
            }
        }

        private void CleanupCallback(object? state)
        {
            var now = DateTime.UtcNow;
            while (_idleEngines.Count > MinPoolSize)
            {
                if (!_idleEngines.TryPeek(out var engine)) break;

                if (now - engine.LastUsedTime > MaxIdleTime)
                {
                    if (_idleEngines.TryTake(out engine))
                    {
                        engine.Engine.Dispose();
                        Interlocked.Decrement(ref _totalEngines);
                    }
                }
                else
                {
                    break;
                }
            }
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
            foreach (var engine in _idleEngines)
            {
                engine.Engine.Dispose();
            }
            foreach (var engine in _activeEngines.Keys)
            {
                engine.Dispose();
            }
        }
    }
}