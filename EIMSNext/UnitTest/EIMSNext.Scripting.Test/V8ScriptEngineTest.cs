using System.Dynamic;

namespace EIMSNext.Scripting.Test
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
    }
}