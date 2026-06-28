using System;
using System.Dynamic;
using System.Collections.Generic;
using System.Threading;

namespace EIMSNext.Scripting.Tests
{
    [TestClass]
    public class V8ScriptEngineTest
    {
        [TestMethod]
        public void TestEval()
        {
            IScriptEngine pool = new V8ScriptEngine(new ScriptEngineOption() { MinPoolSize = 1 });
            var result = pool.Evaluate("CONCAT(TOLOWER(j8sef96v366f_x), TOUPPER(vrg8fk3490cl7))", BuildData());

            Assert.AreEqual("aaWW", result.Value);

            result = pool.Evaluate("( MATCH(subform, x=>{return EQ(x.numbervalue,100)}) )", BuildData());

            Assert.AreEqual(true, result.Value);

            result = pool.Evaluate("(subform[1].numbervalue)", BuildData());

            Assert.AreEqual(200, result.Value);
        }

        [TestMethod]
        public void TestEvalT()
        {
            IScriptEngine pool = new V8ScriptEngine(new ScriptEngineOption() { MinPoolSize = 1 });
            var result = pool.Evaluate<double>("FIXED(ROUND(dxz4j6y7j7w5p,2)+ROUND(g4pzsg8c9ejm9,2),2)", BuildData());

            Assert.AreEqual(3, result.Value);
        }

        private Dictionary<string, object> BuildData()
        {
            var data = new Dictionary<string, object>();

            /*
            {
              "8sef96v366f_x": "a",
              "vrg8fk3490cl7": "w",
              "dxz4j6y7j7w5p": 1,
              "g4pzsg8c9ejm9": 2,
              "w38c_ywlewyom": 3
            }
             */
            data.Add("j8sef96v366f_x", "aA");
            data.Add("vrg8fk3490cl7", "wW");
            data.Add("dxz4j6y7j7w5p", 1);
            data.Add("g4pzsg8c9ejm9", 2);
            data.Add("w38c_ywlewyom", 3);
            var subData = new ExpandoObject();
            subData.TryAdd("inputvalue", "222");
            subData.TryAdd("numbervalue", 100);
            var subData1 = new ExpandoObject();
            subData1.TryAdd("inputvalue", "222");
            subData1.TryAdd("numbervalue", 200);
            data.Add("subform", new[] { subData, subData1 });

            return data;
        }

        [TestMethod]
        public void TestWfEval()
        {
            IScriptEngine pool = new V8ScriptEngine(new ScriptEngineOption() { MinPoolSize = 1 });
            //var data = new Dictionary<string, object>
            //{
            //    { "zr4pr43i4d504", "111" }
            //};
            //var result = pool.Evaluate("( EQ(zr4pr43i4d504,'111') )", data);

            //Assert.AreEqual(true, result);

            var wfResult = pool.Evaluate("( EQ(data.zr4pr43i4d504,'111') )", BuildWfData());
            //var wfResult = pool.Evaluate("VALUE(data,'zr4pr43i4d504')", BuildWfData());

            Assert.AreEqual(true, wfResult.Value);
        }
        private Dictionary<string, object> BuildWfData()
        {
            var wfData = new Dictionary<string, object>();

            //var data = new Dictionary<string, object>();
            //data.Add("zr4pr43i4d504", "111");
            var data = new ExpandoObject();
            data.TryAdd("zr4pr43i4d504", "111");
            wfData.Add("data", data);
            return wfData;
        }
        private Dictionary<string, object> BuildWfSubData()
        {
            var wfData = new Dictionary<string, object>();

            var data = new ExpandoObject();
            data.TryAdd("inputvalue", "111");
            var subData = new ExpandoObject();
            subData.TryAdd("inputvalue", "222");
            subData.TryAdd("numbervalue", 100);
            data.TryAdd("subform1", new List<ExpandoObject> { subData });

            var subData1 = new ExpandoObject();
            subData1.TryAdd("inputvalue", "222");
            subData1.TryAdd("numbervalue", 100);
            var subData2 = new ExpandoObject();
            subData2.TryAdd("inputvalue", "222");
            subData2.TryAdd("numbervalue", 200);
            data.TryAdd("subform2", new List<ExpandoObject> { subData1,subData2 });

            wfData.Add("data", data);
            return wfData;
        }

        [TestMethod]
        public void TestMatch()
        {
            IScriptEngine pool = new V8ScriptEngine(new ScriptEngineOption() { MinPoolSize = 1 });
            var data = BuildWfSubData();
            var wfResult = pool.Evaluate("( EQ(data.inputvalue,'111') && MATCH(data.subform1, x=>{return EQ(x.numbervalue,100)}) )", data);
            wfResult = pool.Evaluate("( EQ(data.inputvalue,'111') && MATCH(data.subform1, x=>{return MATCH(data.subform2, y=>{return EQ(y.numbervalue,x.numbervalue)})}) )", data);

            Assert.AreEqual(true, wfResult.Value);
        }

        [TestMethod]
        public void TestMap()
        {
            IScriptEngine pool = new V8ScriptEngine(new ScriptEngineOption() { MinPoolSize = 1 });
            var data = BuildWfSubData();
            var wfResult = pool.Evaluate("MAP(data.subform2,'inputvalue')", data);
            Assert.AreEqual("[\"222\",\"222\"]", wfResult.Value);
        }

        [TestMethod]
        public void TestColumnAndSum()
        {
            IScriptEngine pool = new V8ScriptEngine(new ScriptEngineOption() { MinPoolSize = 1 });
            var data = BuildWfSubData();
            var wfResult = pool.Evaluate("MAP(data.subform2,'numbervalue')", data);
            Assert.AreEqual("[100,200]", wfResult.Value);
        }

        [TestMethod]
        public void TestSubFieldFormula()
        {
            IScriptEngine pool = new V8ScriptEngine(new ScriptEngineOption() { MinPoolSize = 1 });
            var data = BuildWfSubData();
            var wfResult = pool.Evaluate("CONCAT(data.subform2[0].inputvalue,'-',data.inputvalue)", data);
            Assert.AreEqual("222-111", wfResult.Value);
        }
        [TestMethod]
        public void TestArray()
        {
            IScriptEngine pool = new V8ScriptEngine(new ScriptEngineOption() { MinPoolSize = 1 });
            var data = BuildWfSubData();
            var wfResult = pool.Evaluate("data.subform2[0].numbervalue", data);
            Assert.AreEqual(100, wfResult.Value);
            wfResult = pool.Evaluate("data.subform2[1].numbervalue", data);
            Assert.AreEqual(200, wfResult.Value);
            wfResult = pool.Evaluate("data.subform2[2].numbervalue", data);
            Assert.AreEqual(false, wfResult.Success);
        }

        [TestMethod]
        public void TestEval_WithParameters_HostObject()
        {
            IScriptEngine pool = new V8ScriptEngine(new ScriptEngineOption() { MinPoolSize = 1 });
            var host = new ExpandoObject();
            host.TryAdd("A", 5);
            var parameters = new Dictionary<string, object> { { "data", host } };
            var result = pool.Evaluate<int>("data.A + 1", parameters);
            Assert.AreEqual(6, result.Value);
        }

        [TestMethod]
        public void TestNin_Behaves_As_NotIn()
        {
            IScriptEngine pool = new V8ScriptEngine(new ScriptEngineOption() { MinPoolSize = 1 });
            // 在合集中 → false；不在 → true
            Assert.AreEqual(false, pool.Evaluate("NIN([1,2,3], 2)", null).Value);
            Assert.AreEqual(true, pool.Evaluate("NIN([1,2,3], 4)", null).Value);
            // 与 IN 互为否定
            Assert.AreEqual(true, pool.Evaluate("NIN([1,2,3], 4) === !IN([1,2,3], 4)", null).Value);
        }

        [TestMethod]
        public void TestEvaluate_TimesOut_OnInfiniteLoop()
        {
            // DefaultEvaluationTimeout = 200ms 用来验证默认超时生效
            IScriptEngine pool = new V8ScriptEngine(new ScriptEngineOption
            {
                MinPoolSize = 1,
                DefaultEvaluationTimeout = TimeSpan.FromMilliseconds(200)
            });

            // IIFE 形式让死循环成为合法表达式;V8 引擎会包装成 `(() => { return (expr) })()`。
            var result = pool.Evaluate("(() => { while(true){} })()", null);

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error?.Contains("timed out") == true,
                $"expected 'timed out' in error, got: {result.Error}");
        }

        [TestMethod]
        public void TestEvaluate_Honours_ExplicitCancellationToken()
        {
            IScriptEngine pool = new V8ScriptEngine(new ScriptEngineOption
            {
                MinPoolSize = 1,
                // 显式 CT 模式下,默认超时被覆盖;这里把默认设很大,
                // 验证显式 CT 能在 100ms 内打断死循环。
                DefaultEvaluationTimeout = TimeSpan.FromSeconds(30)
            });

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            var result = pool.Evaluate("(() => { while(true){} })()", null, cts.Token);

            Assert.IsFalse(result.Success);
            // 显式 CT 触发时,IsCancellationRequested=true,Error 是 "cancelled" 而非 "timed out"
            Assert.IsTrue(result.Error?.Contains("cancelled") == true,
                $"expected 'cancelled' in error, got: {result.Error}");
        }

        [TestMethod]
        public void TestEvaluate_DefaultTimeout_Zero_Disables_AutoCancel()
        {
            // DefaultEvaluationTimeout = Zero 时,只有显式 CT 才生效。
            // 短任务不应被错误打断。
            IScriptEngine pool = new V8ScriptEngine(new ScriptEngineOption
            {
                MinPoolSize = 1,
                DefaultEvaluationTimeout = TimeSpan.Zero
            });

            var result = pool.Evaluate("1+1", null);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, result.Value);
        }
    }
}
