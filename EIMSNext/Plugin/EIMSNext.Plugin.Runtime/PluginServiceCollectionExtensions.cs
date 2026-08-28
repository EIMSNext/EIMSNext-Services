using HKH.Mef2.Integration;
using Microsoft.Extensions.DependencyInjection;

namespace EIMSNext.Plugin.Runtime
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
    }
}
