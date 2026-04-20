using EIMSNext.Plugin.Contracts;

namespace EIMSNext.ApiCore.Plugin
{
    public interface IPluginRuntimeManager
    {
        IReadOnlyList<PluginRuntimeInfo> GetPlugins();

        Task<PluginExecResult> ExecuteAsync(
            string pluginId,
            PluginSetting setting,
            PluginExecArgs args,
            PluginInvocationContext? context = null,
            CancellationToken cancellationToken = default);

        Task<PluginReloadResult> ReloadAsync(CancellationToken cancellationToken = default);
    }
}
