using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using EIMSNext.Common.Extension;
using EIMSNext.Core.Serialization;
using EIMSNext.Json.Serialization;

namespace EIMSNext.Core.Test
{
    [TestClass]
    public class JsonSerializerTest
    {
        [TestMethod]
        public void FlexibleEnumTest()
        {
            var opt = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            opt.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
            opt.NumberHandling = JsonNumberHandling.AllowReadingFromString;
            opt.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            opt.PropertyNameCaseInsensitive = true;
            opt.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            opt.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;

            opt.Converters.Add(new BsonDocumentJsonConverter());
            opt.Converters.Add(new ExceptionJsonConverter());
            opt.Converters.Add(new FlexibleEnumConverterFactory());
            opt.Converters.Add(new ObjectJsonConverter());
            opt.Converters.Add(new ExpandoObjectJsonConverter());
            opt.Converters.Add(new UnixMillisecondsDateTimeJsonConverter());

            JsonSerializerExtension.SetOptions(opt);

            var jsonStr = "{\"flagEnums\":15,\"dateTime\":" + DateTime.Today.ToTimeStampMs() + "}";
            var test = jsonStr.DeserializeFromJson<TestClass>();
            Assert.IsNotNull(test);
            Assert.IsTrue(test.FlagEnums.HasFlag(FlagEnum.Value1));
            Assert.IsTrue(test.FlagEnums.HasFlag(FlagEnum.Value2));
            Assert.IsTrue(test.FlagEnums.HasFlag(FlagEnum.Value3));
            Assert.IsTrue(test.FlagEnums.HasFlag(FlagEnum.Value4));
            Assert.AreEqual(DateTime.Today.ToUniversalTime(), test.DateTime);

            Assert.AreEqual(jsonStr, test.SerializeToJson());
        }
    }

    [Flags]
    enum FlagEnum
    {
        None = 0,
        Value1 = 1,
        Value2 = 2,
        Value3 = 4,
        Value4 = 8,
    }
    class TestClass
    {
        public FlagEnum FlagEnums { get; set; }
        public DateTime DateTime { get; set; }
    }
}
