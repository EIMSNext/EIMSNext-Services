using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using HKH.Mef2.Integration;

using EIMSNext.Entity;
using EIMSNext.Scripting;

namespace EIMSNext.Component
{
    public class FormFormulaEvaluator
    {
        private IScriptEngine ScriptEngine { get; set; }
        public FormFormulaEvaluator(IScriptEngine egine)
        {
            ScriptEngine = egine;
        }

        /* TODO: 表内公式有些函数是不支持后端运算的
        public void Evaluate(FormDef formDef, IEnumerable<FormData> formDatas)
        {
            //计算表单内公式字段
            if (formDef.Content.Items?.Count > 0)
            {
                //1. 查找所有公式字段
                var formulaFields = formDef.Content.Items.Where(x => x.Options.ValueOpt != null && !string.IsNullOrEmpty(x.Options.ValueOpt.Formula));
                if (formulaFields.Any())
                {
                    var fieldRanks = new Dictionary<string, double>();
                    var fieldFormulas = new Dictionary<string, string>();

                    int i = 0;
                    foreach (var formulaField in formulaFields)
                    {
                        fieldRanks.Add(formulaField.Field, i);
                        fieldFormulas.Add(formulaField.Field, formulaField.Options.ValueOpt!.Formula!);
                        i++;
                    }

                    //2. 按依赖排序，设置权值
                    foreach (var formulaField in formulaFields)
                    {
                        var depend = formulaField.Options.ValueOpt!.Depends;
                        if (!string.IsNullOrEmpty(depend))
                        {
                            var depends = depend.Split(',', StringSplitOptions.RemoveEmptyEntries);
                            var rank = GetMaxRank(fieldRanks, depends);
                            if (rank > -1) fieldRanks[formulaField.Field] = rank + 0.1;
                        }
                    }

                    //3. 按依赖顺序依次计算
                    foreach (var formData in formDatas)
                    {
                        var formScriptData = GetFormScriptData(formData);
                        fieldRanks.OrderBy(r => r.Value).ForEach(x =>
                        {
                            var val = (object?)FormulaEngine.Evaluate(fieldFormulas[x.Key], formScriptData);
                            formData.Data.TryAdd(x.Key, val);

                            //因为依赖，计算结果传递给下一次计算
                            if (val != null) formScriptData.Add(x.Key, val);
                        });
                    }
                }
            }
        }

        private double GetMaxRank(Dictionary<string, double> fieldRanks, IEnumerable<string> depends)
        {
            double maxRank = -1;

            depends.ForEach(d =>
            {
                if (fieldRanks.ContainsKey(d))
                    maxRank = Math.Max(maxRank, fieldRanks[d]);
            });

            return maxRank;
        }
        */
       
    }
}
