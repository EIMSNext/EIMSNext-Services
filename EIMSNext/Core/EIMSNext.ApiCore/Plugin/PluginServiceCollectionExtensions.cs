using System.Composition.Convention;
using System.Reflection;
using System.Runtime.Loader;

using HKH.Mef2.Integration;

using Microsoft.Extensions.DependencyInjection;

namespace EIMSNext.ApiCore.Plugin
{
    public static class PluginServiceCollectionExtensions
    {
        public static void AddPluginRuntime(this IServiceCollection services, string baseDirectory)
        {
            var pluginRoot = Path.Combine(baseDirectory, "Plugins");
            services.AddSingleton<IPluginRuntimeManager>(serviceProvider =>
            {
                var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PluginRuntimeManager>>();
                var manager = new PluginRuntimeManager(serviceProvider, logger, pluginRoot);
                manager.ReloadAsync().GetAwaiter().GetResult();
                return manager;
            });
        }

        public static void AddGlobalMef(this IServiceCollection services, string dir, string searchPattern = "*.dll")
        {
            var files = Directory.GetFiles(dir, searchPattern, SearchOption.AllDirectories)
                .Where(x => !IsUnderPlugins(dir, x))
                .Where(x => Path.GetFileName(x).StartsWith("EIMSNext.", StringComparison.OrdinalIgnoreCase))
                .Where(x => !Path.GetFileName(x).EndsWith("Plugin.dll", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var mefContainer = new DefaultContainerConfiguration();
            var sharedConventions = new ConventionBuilder();
            sharedConventions.ForTypesMatching(_ => true).Shared();

            mefContainer.WithAssemblies(files.Select(TryLoadComposableAssembly).Where(x => x != null)!, sharedConventions);
            services.EnableMef2(mefContainer);
        }

        private static Assembly? TryLoadComposableAssembly(string assemblyPath)
        {
            try
            {
                var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
                _ = assembly.DefinedTypes.Count();
                return assembly;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsUnderPlugins(string baseDir, string filePath)
        {
            var pluginRoot = Path.Combine(baseDir, "Plugins") + Path.DirectorySeparatorChar;
            return filePath.StartsWith(pluginRoot, StringComparison.OrdinalIgnoreCase);
        }
    }
}
