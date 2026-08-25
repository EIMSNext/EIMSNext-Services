using EIMSNext.Flow.Core.Interfaces;
using EIMSNext.Flow.Core.Nodes;
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

            services.AddTransient<IWorkflowActionService, WorkflowActionService>();
            services.AddTransient<IEventFlowRunner, EventFlowRunner>();

            services.AddTransient<WfStartNode>();
            services.AddTransient<WfApproveNode>();
            services.AddTransient<WfCopyToNode>();
            services.AddTransient<WfEndNode>();

            services.AddTransient<EfStartNode>();
            services.AddTransient<EfEndNode>();
            services.AddTransient<EfQueryOneNode>();
            services.AddTransient<EfQueryManyNode>();
            services.AddTransient<EfInsertNode>();
            services.AddTransient<EfUpdateNode>();
            services.AddTransient<EfDeleteNode>();
            services.AddTransient<EfPrintNode>();
            services.AddTransient<EfPluginNode>();
        }
    }
}
