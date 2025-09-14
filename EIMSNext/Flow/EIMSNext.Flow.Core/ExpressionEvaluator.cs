using System.Dynamic;
using EIMSNext.Core.Repository;
using EIMSNext.Entity;
using EIMSNext.Flow.Core;
using EIMSNext.Flow.Core.Interface;
using EIMSNext.Scripting;
using WorkflowCore.Interface;

namespace EIMSNext.Workflow.Repository
{
    public class ExpressionEvaluator : IExpressionEvaluator
    {
        private readonly IScriptEngine _scriptEngine;
        private readonly IRepository<FormData> _formDataRepository;

        public ExpressionEvaluator(IScriptEngine scriptEngine, IRepository<FormData> formDataRepository)
        {
            _scriptEngine = scriptEngine;
            _formDataRepository = formDataRepository;
        }

        public object? EvaluateExpression(string sourceExpr, object pData, IStepExecutionContext pContext)
        {
            var resolvedValue = _scriptEngine.Evaluate(sourceExpr, new Dictionary<string, object>()
            {
                ["data"] = pData,
                //["context"] = pContext,
                //["environment"] = Environment.GetEnvironmentVariables(),
                //["readFile"] = new Func<string, byte[]>(File.ReadAllBytes),
                //["readText"] = new Func<string, Encoding, string>(File.ReadAllText)
            });
            return resolvedValue.Value;
        }

        public bool EvaluateOutcomeExpression(string sourceExpr, object data, object outcome)
        {
            var wrapData = new ExpandoObject();
            var matchedResult = false;
            var needEval = true;
            if (data is DfDataContext dfDataContext)
            {
                matchedResult = dfDataContext.MatchedResult;

                if (dfDataContext.MatchParallel || !matchedResult)
                {
                    foreach (var item in dfDataContext.NodeDatas)
                    {
                        if (item.Value.ActionDatas.Count > 0)
                        {
                            var actionData = item.Value.ActionDatas.First();
                            var pData = actionData.FormData. Data;
                            pData.TryAdd("createBy", actionData.FormData.CreateBy);

                            wrapData.TryAdd($"n_{item.Value.NodeId}", pData);
                        }
                    }
                }
                else
                {
                    needEval = false;
                }
            }
            else
            {
                var wfDataContext = (ExpandoObject)data;
                matchedResult = wfDataContext.GetValueOrDefault<bool>(WfConsts.MatchedResult);

                if (wfDataContext.GetValueOrDefault<bool>(WfConsts.MatchParallel) || !matchedResult)
                {
                    var formData = _formDataRepository.Get(wfDataContext.GetValue(WfConsts.DataId, string.Empty))!;

                    //TODO: 当前只传入动态数据
                    var pData = formData.Data;

                    pData.TryAdd("createBy", formData.CreateBy);
                    pData.TryAdd(WfConsts.MatchedResult, matchedResult);

                    wrapData.TryAdd($"f_{formData.FormId}", pData);
                }
                else
                {
                    needEval = false;
                }
            }

            if (needEval)
            {
                var resolvedValue = _scriptEngine.Evaluate(sourceExpr, new Dictionary<string, object>()
                {
                    ["data"] = wrapData,
                    //["outcome"] = outcome,
                    //["environment"] = Environment.GetEnvironmentVariables(),
                    //["readFile"] = new Func<string, byte[]>(File.ReadAllBytes),
                    //["readText"] = new Func<string, Encoding, string>(File.ReadAllText)
                });
                bool result = Convert.ToBoolean(resolvedValue.Value);

                if (data is DfDataContext)
                {
                    ((DfDataContext)data).MatchedResult = matchedResult || result;
                }
                else
                {
                    ((ExpandoObject)data).AddOrUpdate(WfConsts.MatchedResult, matchedResult || result);
                }

                return result;
            }

            return false;
        }
    }
}