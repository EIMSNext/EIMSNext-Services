using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

using NLog;

namespace EIMSNext.Plugin.Interface
{
    public interface IPlugin : IDisposable
    {
        PluginDesc Description { get; }
        public PluginExecResult Execute(PluginSetting setting, PluginExecArgs execArgs);
    }

    public abstract class PluginBase<TSetting> : IPlugin where TSetting : class, new()
    {
        protected ILogger Logger = LogManager.GetCurrentClassLogger();
        public TSetting Setting { get; set; } = new TSetting();

        public PluginDesc Description => BuildPluginDesc();
             
        public virtual PluginExecResult Execute(PluginSetting pluginP, PluginExecArgs execArgs)
        {
            if (TryParse(pluginP.Settings, out var setting))
            {
                //TODO: update default setting to json
                Setting = setting.Deserialize<TSetting>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            }

            var result = new PluginExecResult();

            var methodInfo = GetType().GetMethod(execArgs.FunName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (methodInfo == null)
            {
                result.Code = -1;
                result.Message = $"The plugin of [{execArgs.FunName}] no exists";
                return result;
            }

            if (!TryParse(execArgs.FunArgs, out var funArgs))
                funArgs = new JsonObject();
            //TODO: update default setting to json

            ParameterInfo[] parameters = methodInfo.GetParameters();
            if (parameters.Length != 1)
            {
                result.Code = -2;
                result.Message = $"The [{execArgs.FunName}] must have one argument only.";
                return result;
            }
            var parameterType = parameters[0].ParameterType;

            var thisType = this.GetType();
            var instanceParam = Expression.Parameter(thisType, "instance");
            var dataParam = Expression.Parameter(parameterType, "data");

            var delegateType = methodInfo.ReturnType == typeof(void)
           ? typeof(Action<,>).MakeGenericType(thisType, parameterType)
           : typeof(Func<,,>).MakeGenericType(thisType, parameterType, methodInfo.ReturnType);

            var call = Expression.Call(instanceParam, methodInfo, dataParam);
            var funDelegate = Expression.Lambda(delegateType, call, instanceParam, dataParam).Compile();

            var data = funArgs!.Deserialize(parameterType, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            try
            {
                if (methodInfo.ReturnType == typeof(void))
                {
                    funDelegate.DynamicInvoke(this, data);
                }
                else
                {
                    result.Result = funDelegate.DynamicInvoke(this, data);
                }
            }
            catch (Exception ex)
            {
                result.Code = -3;
                result.Message = ex.Message;
                Logger.Error(ex, $"Plugin [{execArgs.FunName}] occurs error with {execArgs.FunArgs}");
            }
            return result;
        }
       
        public virtual void Dispose()
        {
        }

        #region Helper

        protected virtual PluginDesc BuildPluginDesc()
        {
            return new PluginDesc() { Id = "", Name = "aa" };
        }

        protected bool TryParse(string? s, out JsonObject? result)
        {
            result = null;
            try
            {
                if (string.IsNullOrEmpty(s)) result = new JsonObject();
                else result = JsonNode.Parse(s) as JsonObject;

                return result != null;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}
