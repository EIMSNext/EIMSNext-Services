using EIMSNext.Plugin.Contracts;

namespace EIMSNext.Plugin.Runtime
{
    public interface IPluginRuntimeManager
    {
        IReadOnlyList<PluginRuntimeInfo> GetPlugins();

        PluginRuntimeInfo? GetPlugin(string pluginId);

        Task<PluginExecResult> ExecuteAsync(
            string pluginId,
            PluginSetting setting,
            PluginExecArgs args,
            PluginInvocationContext? context = null,
            CancellationToken cancellationToken = default);

        Task<PluginReloadResult> ReloadAsync(CancellationToken cancellationToken = default);
    }
}
