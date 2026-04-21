using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using EIMSNext.Json.Serialization;

namespace EIMSNext.Json.Tests
{
    [TestClass]
    public class ExpandoObjectJsonConverter_Tests
    {
        private JsonSerializerOptions CreateOptions()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExpandoObjectJsonConverter());
            return options;
        }

        [TestMethod]
        public void Deserialize_SimpleObject_ReturnsExpected()
        {
            var json = "{\"name\":\"Alice\",\"age\":30,\"active\":true,\"tags\":[\"a\",\"b\"],\"meta\":{\"id\":1}}";
            var options = CreateOptions();
            var expando = JsonSerializer.Deserialize<ExpandoObject>(json, options);
            var dict = (IDictionary<string, object>)expando!;

            Assert.AreEqual("Alice", dict["name"] as string);
            Assert.AreEqual(30L, dict["age"]);
            Assert.AreEqual(true, dict["active"]);

            var tags = dict["tags"] as List<object>;
            CollectionAssert.AreEqual(new List<object> { "a", "b" }, tags);

            var meta = dict["meta"] as ExpandoObject;
            var metaDict = (IDictionary<string, object>)meta!;
            Assert.AreEqual(1L, metaDict["id"]);
        }

        [TestMethod]
        public void Deserialize_NestedArrayOfObjects_ReturnsExpandoObjects()
        {
            var json = "{\"items\":[{\"id\":1},{\"id\":2}]}";
            var options = CreateOptions();
            var expando = JsonSerializer.Deserialize<ExpandoObject>(json, options);
            var dict = (IDictionary<string, object>)expando!;
            var items = dict["items"] as IList<object>;
            Assert.AreEqual(2, items.Count);
            var first = items[0] as ExpandoObject;
            var firstDict = (IDictionary<string, object>)first!;
            Assert.AreEqual(1L, firstDict["id"]);
        }

        [TestMethod]
        public void PreserveKeyCase()
        {
            var json = "{\"CamelCaseKey\": \"value\"}";
            var options = CreateOptions();
            var expando = JsonSerializer.Deserialize<ExpandoObject>(json, options);
            var dict = (IDictionary<string, object>)expando!;
            Assert.IsTrue(dict.ContainsKey("CamelCaseKey"));
        }

        [TestMethod]
        public void NullValues_AreHandledAsNulls()
        {
            var json = "{\"a\":null,\"b\":[{\"c\":null},5]}";
            var options = CreateOptions();
            var expando = JsonSerializer.Deserialize<ExpandoObject>(json, options);
            var dict = (IDictionary<string, object>)expando!;
            Assert.IsNull(dict["a"]);
            var b = dict["b"] as IList<object>;
            var first = b[0] as ExpandoObject;
            var firstDict = (IDictionary<string, object>)first!;
            Assert.IsNull(firstDict["c"]);
            Assert.AreEqual(5L, b[1]);
        }

        [TestMethod]
        public void Deserialize_MixedTypes_ReturnsCorrectTypes()
        {
            var json = "{\"m\": [\"text\", 123, false, null, {\"inner\": 7}], \"n\": {\"a\": [1, 2, 3]}}";
            var options = CreateOptions();
            var expando = JsonSerializer.Deserialize<ExpandoObject>(json, options);
            var dict = (IDictionary<string, object>)expando!;
            var m = dict["m"] as List<object>;
            Assert.IsNotNull(m);
            Assert.AreEqual("text", m[0] as string);
            Assert.AreEqual(123L, Convert.ToInt64(m[1]));
            Assert.AreEqual(false, m[2]);
            Assert.IsNull(m[3]);
            var inner = m[4] as ExpandoObject;
            var innerDict = (IDictionary<string, object>)inner!;
            Assert.AreEqual(7L, innerDict["inner"]);

            var n = dict["n"] as ExpandoObject;
            var nDict = (IDictionary<string, object>)n!;
            var a = nDict["a"] as List<object>;
            CollectionAssert.AreEqual(new List<object> { 1L, 2L, 3L }, a);
        }
    }
}
