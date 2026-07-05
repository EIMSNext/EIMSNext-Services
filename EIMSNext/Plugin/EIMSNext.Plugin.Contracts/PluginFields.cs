namespace EIMSNext.Plugin.Contracts
{
    public interface IPluginField
    {
    }

    public abstract class PluginField : IPluginField
    {
    }

    public abstract class PluginSubList<T1> : PluginField
        where T1 : IPluginField
    {
    }

    public abstract class PluginSubList<T1, T2> : PluginField
        where T1 : IPluginField
        where T2 : IPluginField
    {
    }

    public abstract class PluginSubList<T1, T2, T3> : PluginField
        where T1 : IPluginField
        where T2 : IPluginField
        where T3 : IPluginField
    {
    }

    public abstract class PluginSubList<T1, T2, T3, T4> : PluginField
        where T1 : IPluginField
        where T2 : IPluginField
        where T3 : IPluginField
        where T4 : IPluginField
    {
    }
}
