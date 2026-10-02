using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;

using EIMSNext.Plugin.Contracts;

using HKH.Mef2.Integration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EIMSNext.Plugin.Runtime
{
    public sealed class PluginRuntimeManager : IPluginRuntimeManager
    {
        internal static readonly TimeSpan DefaultExecutionTimeout = TimeSpan.FromSeconds(30);
        internal static readonly TimeSpan PluginCancellationGracePeriod = TimeSpan.FromSeconds(1);

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PluginRuntimeManager> _logger;
        private readonly string _pluginRoot;
        private readonly TimeSpan _executionTimeout;
        private ImmutableDictionary<string, PluginRuntime> _activeRuntimes = ImmutableDictionary<string, PluginRuntime>.Empty.WithComparers(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim _reloadLock = new(1, 1);
        private readonly Func<PluginAssemblyCandidate, PluginRuntime> _runtimeFactory;

        public PluginRuntimeManager(IServiceProvider serviceProvider, ILogger<PluginRuntimeManager> logger, string pluginRoot, TimeSpan? executionTimeout = null)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _pluginRoot = pluginRoot;
            _executionTimeout = executionTimeout ?? DefaultExecutionTimeout;
            _runtimeFactory = CreateRuntime;
        }

        internal PluginRuntimeManager(IServiceProvider serviceProvider, ILogger<PluginRuntimeManager> logger, string pluginRoot, Func<PluginAssemblyCandidate, PluginRuntime> runtimeFactory, TimeSpan? executionTimeout = null)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _pluginRoot = pluginRoot;
            _executionTimeout = executionTimeout ?? DefaultExecutionTimeout;
            _runtimeFactory = runtimeFactory;
        }

        public IReadOnlyList<PluginRuntimeInfo> GetPlugins()
        {
            return _activeRuntimes.Values
                .OrderBy(x => x.PluginId, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.ToRuntimeInfo())
                .ToList();
        }

        public PluginRuntimeInfo? GetPlugin(string pluginId)
        {
            return ResolveRuntime(pluginId)?.ToRuntimeInfo();
        }

        public async Task<PluginExecResult> ExecuteAsync(string pluginId, PluginSetting setting, PluginExecArgs args, PluginInvocationContext? context = null, CancellationToken cancellationToken = default)
        {
            var runtime = ResolveRuntime(pluginId);
            if (runtime == null)
            {
                return new PluginExecResult { Code = -404, Message = $"Plugin [{pluginId}] not found." };
            }

            return await runtime.ExecuteAsync(setting, args, context, cancellationToken, _executionTimeout);
        }

        public async Task<PluginReloadResult> ReloadAsync(CancellationToken cancellationToken = default)
        {
            await _reloadLock.WaitAsync(cancellationToken);
            try
            {
                var result = new PluginReloadResult();
                var candidates = DiscoverPluginCandidates();
                var newMap = _activeRuntimes;
                var retiredRuntimes = new List<PluginRuntime>();

                foreach (var candidate in candidates)
                {
                    _activeRuntimes.TryGetValue(candidate.PluginId, out var currentRuntime);
                    if (currentRuntime != null && currentRuntime.Version >= candidate.Version)
                    {
                        result.Items.Add(new PluginReloadItemResult
                        {
                            PluginId = candidate.PluginId,
                            PreviousVersion = currentRuntime.Version.ToString(),
                            CurrentVersion = currentRuntime.Version.ToString(),
                            Updated = false,
                            UnloadedOldVersion = true,
                            Message = "Already at latest version."
                        });
                        continue;
                    }

                    var runtime = _runtimeFactory(candidate);
                    newMap = newMap.SetItem(candidate.PluginId, runtime);
                    if (currentRuntime != null)
                    {
                        retiredRuntimes.Add(currentRuntime);
                    }

                    result.Items.Add(new PluginReloadItemResult
                    {
                        PluginId = candidate.PluginId,
                        PreviousVersion = currentRuntime?.Version.ToString(),
                        CurrentVersion = runtime.Version.ToString(),
                        Updated = true,
                        UnloadedOldVersion = currentRuntime == null,
                        Message = "Reloaded latest version."
                    });
                }

                Interlocked.Exchange(ref _activeRuntimes, newMap);

                for (var index = 0; index < retiredRuntimes.Count; index++)
                {
                    var retired = retiredRuntimes[index];
                    retired.Retire();
                    var unloaded = await retired.TryUnloadAsync(TimeSpan.FromSeconds(10), cancellationToken);
                    var item = result.Items.FirstOrDefault(x => string.Equals(x.PluginId, retired.PluginId, StringComparison.OrdinalIgnoreCase));
                    if (item != null)
                    {
                        item.UnloadedOldVersion = unloaded;
                        if (!unloaded)
                        {
                            item.Message = "Reloaded but old version is still referenced.";
                        }
                    }
                }

                return result;
            }
            finally
            {
                _reloadLock.Release();
            }
        }

        private IEnumerable<PluginAssemblyCandidate> DiscoverPluginCandidates()
        {
            if (!Directory.Exists(_pluginRoot))
            {
                return Enumerable.Empty<PluginAssemblyCandidate>();
            }

            return Directory.GetFiles(_pluginRoot, "*Plugin.dll", SearchOption.AllDirectories)
                .Select(CreateCandidate)
                .Where(x => x != null)
                .GroupBy(x => x!.PluginId, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.OrderByDescending(y => y!.Version).First()!)
                .ToList();
        }

        private PluginRuntime? ResolveRuntime(string pluginId)
        {
            return string.IsNullOrWhiteSpace(pluginId) ? null : _activeRuntimes.GetValueOrDefault(pluginId);
        }

        internal PluginAssemblyCandidate? CreateCandidate(string assemblyPath)
        {
            var versionDirectory = Directory.GetParent(assemblyPath);
            var pluginDirectory = versionDirectory?.Parent;
            if (versionDirectory == null || pluginDirectory == null)
            {
                return null;
            }

            if (!PluginVersion.TryParse(versionDirectory.Name, out var version))
            {
                _logger.LogWarning("Skip plugin [{AssemblyPath}] because version directory is invalid.", assemblyPath);
                return null;
            }

            return new PluginAssemblyCandidate
            {
                PluginId = pluginDirectory.Name,
                Version = version,
                VersionText = versionDirectory.Name,
                AssemblyPath = assemblyPath,
            };
        }

        private PluginRuntime CreateRuntime(PluginAssemblyCandidate candidate)
        {
            var loadContext = new PluginLoadContext(candidate.AssemblyPath);
            var assembly = loadContext.LoadFromAssemblyPath(candidate.AssemblyPath);

            // 精确选择"非抽象、可实例化、实现了 IPlugin"的类型。
            // 旧实现用 .First() 在多/零匹配时分别产生 InvalidOperationException / 静默取错类型。
            var pluginType = assembly.GetTypes()
                .Where(t => !t.IsAbstract
                            && !t.IsInterface
                            && typeof(IPlugin).IsAssignableFrom(t)
                            && t.GetConstructor(Type.EmptyTypes) != null)
                .ToList();

            if (pluginType.Count == 0)
            {
                _logger.LogWarning(
                    "Skip plugin [{PluginId}] [{AssemblyPath}]: no concrete IPlugin implementation found.",
                    candidate.PluginId, candidate.AssemblyPath);
                throw new InvalidOperationException(
                    $"插件程序集 [{candidate.AssemblyPath}] 中没有可实例化的 IPlugin 实现");
            }

            if (pluginType.Count > 1)
            {
                _logger.LogWarning(
                    "Plugin assembly [{AssemblyPath}] declares {Count} IPlugin implementations; refusing to load. PluginId={PluginId}",
                    candidate.AssemblyPath, pluginType.Count, candidate.PluginId);
                throw new InvalidOperationException(
                    $"插件程序集 [{candidate.AssemblyPath}] 中包含 {pluginType.Count} 个 IPlugin 实现，无法确定加载哪一个");
            }

            var concrete = pluginType[0];
            using var instance = (IPlugin)Activator.CreateInstance(concrete)!;
            var desc = instance.Description;

            return new PluginRuntime(_serviceProvider, _logger, candidate.PluginId, candidate.Version, candidate.AssemblyPath, concrete, desc, loadContext);
        }

        internal sealed class PluginAssemblyCandidate
        {
            public required string PluginId { get; init; }
            public required PluginVersion Version { get; init; }
            public required string VersionText { get; init; }
            public required string AssemblyPath { get; init; }
        }

        internal sealed class PluginLoadContext : AssemblyLoadContext
        {
            private readonly AssemblyDependencyResolver _resolver;
            private static readonly HashSet<string> SharedAssemblyNames = new(StringComparer.OrdinalIgnoreCase)
            {
                typeof(IPlugin).Assembly.GetName().Name!
            };

            public PluginLoadContext(string pluginAssemblyPath)
                : base($"plugin:{Path.GetFileNameWithoutExtension(pluginAssemblyPath)}", isCollectible: true)
            {
                _resolver = new AssemblyDependencyResolver(pluginAssemblyPath);
            }

            protected override Assembly? Load(AssemblyName assemblyName)
            {
                if (SharedAssemblyNames.Contains(assemblyName.Name ?? string.Empty))
                {
                    return null;
                }

                var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
                return assemblyPath == null ? null : LoadFromAssemblyPath(assemblyPath);
            }

            protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
            {
                var dllPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
                return dllPath == null ? IntPtr.Zero : LoadUnmanagedDllFromPath(dllPath);
            }
        }

        internal sealed class PluginRuntime
        {
            private readonly IServiceProvider _serviceProvider;
            private readonly ILogger _logger;
            private Type? _pluginType;
            private PluginLoadContext? _loadContext;
            private readonly WeakReference _weakReference;
            private int _activeCalls;
            private int _retired;

            public PluginRuntime(IServiceProvider serviceProvider, ILogger logger, string pluginId, PluginVersion version, string assemblyPath, Type pluginType, PluginDesc description, PluginLoadContext loadContext)
            {
                _serviceProvider = serviceProvider;
                _logger = logger;
                PluginId = pluginId;
                Version = version;
                AssemblyPath = assemblyPath;
                _pluginType = pluginType;
                Description = description;
                _loadContext = loadContext;
                _weakReference = new WeakReference(loadContext, trackResurrection: false);
            }

            public string PluginId { get; }
            public PluginVersion Version { get; }
            public string AssemblyPath { get; }
            public PluginDesc Description { get; }

            public PluginRuntimeInfo ToRuntimeInfo()
            {
                return new PluginRuntimeInfo
                {
                    PluginId = PluginId,
                    Name = Description.Name,
                    Version = Description.Version,
                    Description = Description.Description,
                    Functions = Description.Functions.ToList()
                };
            }

            public void Retire()
            {
                Interlocked.Exchange(ref _retired, 1);
            }

            public async Task<PluginExecResult> ExecuteAsync(PluginSetting setting, PluginExecArgs args, PluginInvocationContext? context, CancellationToken cancellationToken, TimeSpan timeout)
            {
                if (Volatile.Read(ref _retired) == 1)
                {
                    return new PluginExecResult { Code = -409, Message = $"Plugin [{PluginId}] is reloading." };
                }

                var pluginType = _pluginType;
                if (pluginType == null)
                {
                    return new PluginExecResult { Code = -409, Message = $"Plugin [{PluginId}] is unloading." };
                }

                // 同步插件无法被强制中止：超时只是停止等待，取消信号只给协作式插件一个退出机会。
                var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                Task<PluginExecResult>? execution = null;
                Interlocked.Increment(ref _activeCalls);
                try
                {
                    await Task.Yield();
                    cancellationToken.ThrowIfCancellationRequested();

                    // 作用域必须建在任务内部：超时后调用方立即返回，建在外面的话 scope 会先被释放，
                    // 而插件线程还在用它解析出来的服务。
                    execution = Task.Run(() =>
                    {
                        using var scope = _serviceProvider.CreateScope();
                        context ??= new PluginInvocationContext();
                        context.Resolver ??= scope.ServiceProvider.GetRequiredService<IResolver>();
                        context.CancellationToken = timeoutCts.Token;
                        var plugin = (IPlugin)Activator.CreateInstance(pluginType)!;
                        return plugin.Execute(setting, args, context);
                    }, timeoutCts.Token);

                    return await execution.WaitAsync(timeout, cancellationToken);
                }
                catch (TimeoutException) when (execution is { IsCompleted: false })
                {
                    // 先通知插件协作式退出，再给它一个短暂宽限期。无论插件是否响应，
                    // 调用方都收到统一的 -408；未退出的插件由 active-call continuation 继续跟踪。
                    timeoutCts.Cancel();
                    try
                    {
                        await execution.WaitAsync(PluginCancellationGracePeriod, CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (TimeoutException)
                    {
                        // 宽限期耗尽，finally 中的 continuation 会继续跟踪未结束的插件任务。
                    }
                    catch (OperationCanceledException)
                    {
                        // 插件响应取消，超时结果仍保持 -408。
                    }
                    catch (Exception ex)
                    {
                        // 插件在响应取消时自行失败；此处已观察异常，避免未观察异常丢失。
                        _logger.LogError(ex, "Plugin [{PluginId}] faulted during cancellation grace period.", PluginId);
                    }

                    return new PluginExecResult
                    {
                        Code = -408,
                        Message = $"Plugin [{PluginId}] execution timed out after {timeout.TotalSeconds:0.##}s.",
                    };
                }
                finally
                {
                    if (execution is { IsCompleted: false })
                    {
                        // 插件线程还在跑：等它真正结束再释放计数，否则热重载会在它运行期间卸载程序集。
                        _ = execution.ContinueWith(task =>
                        {
                            Interlocked.Decrement(ref _activeCalls);
                            timeoutCts.Dispose();
                            if (task.Exception != null)
                            {
                                _logger.LogError(task.Exception, "Plugin [{PluginId}] faulted after the caller stopped waiting.", PluginId);
                            }
                        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                    }
                    else
                    {
                        Interlocked.Decrement(ref _activeCalls);
                        timeoutCts.Dispose();
                    }
                }
            }

            public async Task<bool> TryUnloadAsync(TimeSpan timeout, CancellationToken cancellationToken)
            {
                var stopAt = DateTime.UtcNow.Add(timeout);
                while (Volatile.Read(ref _activeCalls) > 0 && DateTime.UtcNow < stopAt)
                {
                    await Task.Delay(100, cancellationToken);
                }

                _pluginType = null;
                var loadContext = _loadContext;
                _loadContext = null;
                loadContext?.Unload();
                loadContext = null;

                for (var i = 0; i < 10 && _weakReference.IsAlive; i++)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    await Task.Delay(100, cancellationToken);
                }

                if (_weakReference.IsAlive)
                {
                    _logger.LogWarning("Plugin [{PluginId}] old load context is still alive after reload.", PluginId);
                }

                return !_weakReference.IsAlive;
            }
        }

        internal void SetActiveRuntimesForTest(IEnumerable<PluginRuntime> runtimes)
        {
            _activeRuntimes = runtimes.ToImmutableDictionary(x => x.PluginId, StringComparer.OrdinalIgnoreCase);
        }
    }
}
