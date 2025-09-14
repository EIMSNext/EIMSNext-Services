using EIMSNext.Scripting;

using Microsoft.Extensions.DependencyInjection;

namespace EIMSNext.Component
{
    public static class ServiceCollectionExtensions
    {
        public static void AddServiceComponents(this IServiceCollection services)
        {
            services.AddSingleton<IScriptEngine, V8ScriptEngine>();
            services.AddSingleton<FormFormulaEvaluator>();
            services.AddSingleton<WfMetadataParser>();
            services.AddSingleton<FormLayoutParser>();
        }
    }
}
