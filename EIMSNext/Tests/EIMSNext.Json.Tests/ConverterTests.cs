using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using EIMSNext.Json.Serialization;

namespace EIMSNext.Json.Tests
{
    public enum SampleEnum { A = 1, B = 2 }

    [TestClass]
    public class ConverterTests
    {
        [TestMethod]
        public void ExceptionJsonConverter_Serializes_Message()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExceptionJsonConverter());
            var json = JsonSerializer.Serialize(new InvalidOperationException("boom"), options);
            Assert.AreEqual("\"boom\"", json);
        }

        [TestMethod]
        public void FlexibleEnumConverter_SerializeAndDeserialize()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new FlexibleEnumConverterFactory());

            // Serialize as number
            var json = JsonSerializer.Serialize(SampleEnum.A, options);
            Assert.AreEqual("1", json);

            // Deserialize from numeric string
            var v1 = JsonSerializer.Deserialize<SampleEnum>("2", options);
            Assert.AreEqual(SampleEnum.B, v1);

            // Deserialize from named string
            var v2 = JsonSerializer.Deserialize<SampleEnum>("\"A\"", options);
            Assert.AreEqual(SampleEnum.A, v2);
        }

        [TestMethod]
        public void ObjectJsonConverter_DeserializationToDictionary()
        {
            var json = "{\"a\":1,\"b\":\"str\",\"c\":true}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ObjectJsonConverter());
            var obj = JsonSerializer.Deserialize<object>(json, options) as Dictionary<string, object>;
            Assert.IsNotNull(obj);
            Assert.AreEqual(1L, Convert.ToInt64(obj["a"]));
            Assert.AreEqual("str", obj["b"]);
            Assert.AreEqual(true, obj["c"]);
        }

        [TestMethod]
        public void UnixMillisecondsDateTimeJsonConverter_RoundTrip()
        {
            var dt = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            var options = new JsonSerializerOptions();
            options.Converters.Add(new UnixMillisecondsDateTimeJsonConverter());
            var json = JsonSerializer.Serialize(dt, options);
            Assert.IsNotNull(json);
            var dt2 = JsonSerializer.Deserialize<DateTime>(json, options);
            Assert.AreEqual(dt, dt2);
        }

        [TestMethod]
        public void UnixMillisecondsDateTimeJsonConverter_ReadFromRawNumber()
        {
            var ms = 1609459200000L; // 2021-01-01T00:00:00Z
            var options = new JsonSerializerOptions();
            options.Converters.Add(new UnixMillisecondsDateTimeJsonConverter());
            var dt = JsonSerializer.Deserialize<DateTime>(ms.ToString(), options);
            // The above passes a string; ensure it still works by converting back using FromUnixTime
            var expected = DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
            // dt should equal expected when deserialized from number (string path also supported by JsonSerializer)
            Assert.AreEqual(expected, dt);
        }
    }
}
