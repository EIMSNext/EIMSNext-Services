using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EIMSNext.Plugin.Runtime
{
    public static class PluginServiceCollectionExtensions
    {
        public static void AddPluginRuntime(this IServiceCollection services, string baseDirectory, TimeSpan? executionTimeout = null)
        {
            var pluginRoot = Path.Combine(baseDirectory, "Plugins");
            services.AddSingleton<IPluginRuntimeManager>(serviceProvider =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<PluginRuntimeManager>>();
                var manager = new PluginRuntimeManager(serviceProvider, logger, pluginRoot, executionTimeout ?? ReadTimeout(serviceProvider));
                manager.ReloadAsync().GetAwaiter().GetResult();
                return manager;
            });
        }

        private static TimeSpan ReadTimeout(IServiceProvider serviceProvider)
        {
            var seconds = serviceProvider.GetService<IConfiguration>()?.GetValue<int?>("Plugin:ExecutionTimeoutSeconds");
            return seconds is > 0
                ? TimeSpan.FromSeconds(seconds.Value)
                : PluginRuntimeManager.DefaultExecutionTimeout;
        }
    }
}
