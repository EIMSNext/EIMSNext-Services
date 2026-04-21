using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;

using EIMSNext.Plugin.Contracts;

using HKH.Mef2.Integration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EIMSNext.ApiCore.Plugin
{
    public sealed class PluginRuntimeManager : IPluginRuntimeManager
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PluginRuntimeManager> _logger;
        private readonly string _pluginRoot;
        private ImmutableDictionary<string, PluginRuntime> _activeRuntimes = ImmutableDictionary<string, PluginRuntime>.Empty.WithComparers(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim _reloadLock = new(1, 1);
        private readonly Func<PluginAssemblyCandidate, PluginRuntime> _runtimeFactory;

        public PluginRuntimeManager(IServiceProvider serviceProvider, ILogger<PluginRuntimeManager> logger, string pluginRoot)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _pluginRoot = pluginRoot;
            _runtimeFactory = CreateRuntime;
        }

        internal PluginRuntimeManager(IServiceProvider serviceProvider, ILogger<PluginRuntimeManager> logger, string pluginRoot, Func<PluginAssemblyCandidate, PluginRuntime> runtimeFactory)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _pluginRoot = pluginRoot;
            _runtimeFactory = runtimeFactory;
        }

        public IReadOnlyList<PluginRuntimeInfo> GetPlugins()
        {
            return _activeRuntimes.Values
                .OrderBy(x => x.PluginId, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.ToRuntimeInfo())
                .ToList();
        }

        public async Task<PluginExecResult> ExecuteAsync(string pluginId, PluginSetting setting, PluginExecArgs args, PluginInvocationContext? context = null, CancellationToken cancellationToken = default)
        {
            if (!_activeRuntimes.TryGetValue(pluginId, out var runtime))
            {
                return new PluginExecResult { Code = -404, Message = $"Plugin [{pluginId}] not found." };
            }

            return await runtime.ExecuteAsync(setting, args, context, cancellationToken);
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
                        UnloadedOldVersion = false,
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

        internal PluginAssemblyCandidate? CreateCandidate(string assemblyPath)
        {
            var versionDirectory = Directory.GetParent(assemblyPath);
            var pluginDirectory = versionDirectory?.Parent;
            if (versionDirectory == null || pluginDirectory == null)
            {
                return null;
            }

            if (!Version.TryParse(versionDirectory.Name, out var version))
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
            var pluginType = assembly.GetTypes().First(x => !x.IsAbstract && typeof(IPlugin).IsAssignableFrom(x));
            using var instance = (IPlugin)Activator.CreateInstance(pluginType)!;
            var desc = instance.Description;

            return new PluginRuntime(_serviceProvider, _logger, candidate.PluginId, candidate.Version, candidate.AssemblyPath, pluginType, desc, loadContext);
        }

        internal sealed class PluginAssemblyCandidate
        {
            public required string PluginId { get; init; }
            public required Version Version { get; init; }
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

            public PluginRuntime(IServiceProvider serviceProvider, ILogger logger, string pluginId, Version version, string assemblyPath, Type pluginType, PluginDesc description, PluginLoadContext loadContext)
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
            public Version Version { get; }
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

            public async Task<PluginExecResult> ExecuteAsync(PluginSetting setting, PluginExecArgs args, PluginInvocationContext? context, CancellationToken cancellationToken)
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

                Interlocked.Increment(ref _activeCalls);
                try
                {
                    await Task.Yield();
                    cancellationToken.ThrowIfCancellationRequested();
                    using var scope = _serviceProvider.CreateScope();
                    context ??= new PluginInvocationContext();
                    context.Resolver ??= scope.ServiceProvider.GetRequiredService<IResolver>();
                    var plugin = (IPlugin)Activator.CreateInstance(pluginType)!;
                    return plugin.Execute(setting, args, context);
                }
                finally
                {
                    Interlocked.Decrement(ref _activeCalls);
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
