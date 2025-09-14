using EIMSNext.Flow.Core.Interface;
using EIMSNext.Flow.Core.Node;
using EIMSNext.Scripting;
using EIMSNext.Workflow.Repository;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EIMSNext.Flow.Core
{
    public static class ServiceCollectionExtensions
    {
        public static void AddStepBodys(this IServiceCollection services)
        {
            services.AddSingleton<ScriptEngineOption>((s) =>
            {
                var opt = new ScriptEngineOption();
                var sec = s.GetRequiredService<IConfiguration>().GetSection("ScriptEngine");
                if (sec != null)
                    sec.Bind(opt);

                return opt;
            });

            services.AddSingleton<IExpressionEvaluator, ExpressionEvaluator>();

            services.AddTransient<IDataflowRunner, DataflowRunner>();

            services.AddTransient<WfStartNode>();
            services.AddTransient<WfApproveNode>();
            services.AddTransient<WfCopyToNode>();
            services.AddTransient<WfEndNode>();

            services.AddTransient<DfStartNode>();
            services.AddTransient<DfEndNode>();
            services.AddTransient<DfQueryOneNode>();
            services.AddTransient<DfQueryManyNode>();
            services.AddTransient<DfInsertNode>();
            services.AddTransient<DfUpdateNode>();
            services.AddTransient<DfDeleteNode>();
            services.AddTransient<DfPrintNode>();
        }
    }
}
