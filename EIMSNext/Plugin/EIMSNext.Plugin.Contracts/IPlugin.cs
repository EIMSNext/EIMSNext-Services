using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Serilog;

namespace EIMSNext.Plugin.Contracts
{
    public interface IPlugin : IDisposable
    {
        PluginDesc Description { get; }
        public PluginExecResult Execute(PluginSetting setting, PluginExecArgs execArgs, PluginInvocationContext? context = null);
    }

    public abstract class PluginBase<TSetting> : IPlugin where TSetting : class, new()
    {
        protected ILogger Logger => Log.ForContext(GetType());
        public TSetting Setting { get; set; } = new TSetting();
        protected PluginInvocationContext? Context { get; private set; }

        public PluginDesc Description => BuildPluginDesc();
             
        public virtual PluginExecResult Execute(PluginSetting pluginP, PluginExecArgs execArgs, PluginInvocationContext? context = null)
        {
            Context = context;
            if (TryParse(pluginP.Settings, out var setting) && setting != null)
            {
                Setting = setting.DeserializeFromJson<TSetting>()!;
            }

            var result = new PluginExecResult();

            var methodInfo = PluginDescriptionBuilder.FindFunction(GetType(), execArgs.FunName);

            if (methodInfo == null)
            {
                result.Code = -1;
                result.Message = $"The plugin of [{execArgs.FunName}] no exists";
                return result;
            }

            if (!TryParse(execArgs.FunArgs, out var funArgs))
                funArgs = new JsonObject();
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

            var data = PluginValueBinder.Deserialize(funArgs!, parameterType);
            try
            {
                if (methodInfo.ReturnType == typeof(void))
                {
                    funDelegate.DynamicInvoke(this, data);
                }
                else
                {
                    var execResult = funDelegate.DynamicInvoke(this, data);
                    result.Result = PluginDescriptionBuilder.ProjectResult(methodInfo, execResult);
                }
            }
            catch (Exception ex)
            {
                result.Code = -3;
                result.Message = ex.Message;
                Logger.Error(ex, "Plugin execution failed. Function={FunctionName}, Args={FunctionArgs}", execArgs.FunName, execArgs.FunArgs);
            }
            finally
            {
                Context = null;
            }
            return result;
        }
       
        public virtual void Dispose()
        {
        }

        #region Helper

        protected virtual PluginDesc BuildPluginDesc()
        {
            return PluginDescriptionBuilder.Build(GetType());
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
