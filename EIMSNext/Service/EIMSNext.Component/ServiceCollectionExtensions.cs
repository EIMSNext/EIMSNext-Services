using EIMSNext.Scripting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EIMSNext.Component
{
    public static class ServiceCollectionExtensions
    {
        public static void AddServiceComponents(this IServiceCollection services)
        {
            services.AddSingleton<ScriptEngineOption>(serviceProvider =>
            {
                var option = new ScriptEngineOption();
                serviceProvider.GetRequiredService<IConfiguration>()
                    .GetSection("ScriptEngine")
                    .Bind(option);
                return option;
            });
            services.AddSingleton<IScriptEngine, V8ScriptEngine>();
            services.AddSingleton<FormFormulaEvaluator>();
            services.AddSingleton<WfMetadataParser>();
            services.AddSingleton<FormLayoutParser>();
            services.AddSingleton<DataTitleResolver>();
        }
    }
}
