using System.Text.Json;
using EIMSNext.Async.Tasks;
using EIMSNext.Core.Abstractions.Extensions;
using EIMSNext.Json.Serialization;
using EIMSNext.Persistence.PostgreSql;

namespace EIMSNext.Async.Tests
{
    /// <summary>
    /// 动态字段值（FormData.Data）的跨路径类型一致性守门测试。
    /// </summary>
    /// <remarks>
    /// 同一份 JSON 会经三条互不相同的路径进入 <c>FormData.Data</c>：
    /// <list type="bullet">
    /// <item>DB 读回：持久化层 <see cref="DynamicJsonbReader"/>（jsonb → Dictionary）；</item>
    /// <item>API 请求：Json 层 <see cref="DictionaryJsonConverter"/>；</item>
    /// <item>Excel 导入：<c>ImportCellConverters</c>（JsonElement / 单元格数值）。</item>
    /// </list>
    /// 历史上这三处各写了一份还原规则，数值档位不一致（有的没有 decimal、有的多一个 int 档），
    /// 导致同一个 12.5 在导入后是 <see cref="double"/>、读回后是 <see cref="decimal"/>；
    /// 变更日志用 <c>Equals</c> 比较（<c>Equals(12.5m, 12.5d) == false</c>），
    /// 于是每次保存都会多出一条并不存在的修改记录。本测试锁定这个契约。
    /// </remarks>
    [TestClass]
    public class DynamicValueParityTests
    {
        private const string SampleJson = """
        {"intField":7,"decimalField":12.5,"stringField":"x","boolField":true,"nullField":null,
         "nested":{"a":1,"b":2.5},"array":[1,"two",{"c":3}]}
        """;

        [TestMethod]
        public void DbAndRequestPaths_ProduceSameClrTypes()
        {
            var fromDb = DynamicJsonbReader.Parse(SampleJson)!;
            var fromRequest = DeserializeWithConverter(SampleJson);

            AssertSameTypes("db vs request", fromDb, fromRequest);
        }

        [TestMethod]
        public void ImportPath_ProduceSameClrTypes()
        {
            var fromDb = DynamicJsonbReader.Parse(SampleJson)!;

            using var document = JsonDocument.Parse(SampleJson);
            var fromImport = ImportCellConverters.UnwrapJsonValue(document.RootElement) as Dictionary<string, object?>;

            Assert.IsNotNull(fromImport);
            AssertSameTypes("db vs import", fromDb, fromImport);
        }

        [TestMethod]
        public void NumberContract_IsLongThenDecimal()
        {
            var data = DynamicJsonbReader.Parse(SampleJson)!;

            Assert.AreEqual(7L, data["intField"], "整数必须是 long");
            Assert.AreEqual(12.5m, data["decimalField"], "小数必须是 decimal");
            Assert.AreEqual(1L, ((Dictionary<string, object?>)data["nested"]!)["a"]);
            Assert.AreEqual(2.5m, ((Dictionary<string, object?>)data["nested"]!)["b"]);
        }

        [TestMethod]
        public void ObjectPath_UsesTheSameNumberContract()
        {
            // object 声明成员走 ObjectJsonConverter，规则必须与 Dictionary 路径一致。
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ObjectJsonConverter());

            var raw = JsonSerializer.Deserialize<RawHolder>("{\"Root\":" + SampleJson + "}", options)!;
            var values = (Dictionary<string, object?>)raw.Root!;

            Assert.AreEqual(7L, values["intField"]);
            Assert.AreEqual(12.5m, values["decimalField"]);
        }

        [TestMethod]
        public void SameValuesFromDifferentPaths_AreNotReportedAsChanged()
        {
            // 回归用例：导入的数值与 DB 读回的数值类型不同时，曾经会产生虚假变更日志。
            var fromDb = DynamicJsonbReader.Parse(SampleJson)!;

            using var document = JsonDocument.Parse(SampleJson);
            var fromImport = (Dictionary<string, object?>)ImportCellConverters.UnwrapJsonValue(document.RootElement)!;

            var changes = ExpandoComparer.Compare(fromDb, fromImport);
            Assert.AreEqual(0, changes.Count, "同一份数据在两条路径上不应判出变更：" + string.Join(", ", changes.Select(x => $"{x.FieldId}:{x.ChangeType}")));
        }

        [TestMethod]
        public void ImportedNumbers_NormalizeToLongAndDecimal()
        {
            Assert.AreEqual(123L, ImportCellConverters.ConvertNumber(null, "123"));
            Assert.AreEqual(12.5m, ImportCellConverters.ConvertNumber(null, "12.5"));
            Assert.AreEqual(7L, ImportCellConverters.ConvertEditableNumber(7));
            Assert.AreEqual(7.5m, ImportCellConverters.ConvertEditableNumber(7.5f));
        }

        private static Dictionary<string, object?> DeserializeWithConverter(string json)
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new DictionaryJsonConverter());
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(json, options)!;
        }

        private static void AssertSameTypes(string scope, IDictionary<string, object?> expected, IDictionary<string, object?> actual)
        {
            Assert.AreEqual(expected.Count, actual.Count, $"{scope}: 键数量不一致");

            foreach (var (key, expectedValue) in expected)
            {
                Assert.IsTrue(actual.TryGetValue(key, out var actualValue), $"{scope}: 缺少键 {key}");
                AssertSameValue($"{scope}:{key}", expectedValue, actualValue);
            }
        }

        private static void AssertSameValue(string scope, object? expected, object? actual)
        {
            if (expected is null || actual is null)
            {
                Assert.AreEqual(expected, actual, scope);
                return;
            }

            Assert.AreEqual(expected.GetType(), actual.GetType(), $"{scope}: CLR 类型不一致");

            switch (expected)
            {
                case IDictionary<string, object?> expectedDict when actual is IDictionary<string, object?> actualDict:
                    AssertSameTypes(scope, expectedDict, actualDict);
                    break;
                case IReadOnlyList<object?> expectedList when actual is IReadOnlyList<object?> actualList:
                    Assert.AreEqual(expectedList.Count, actualList.Count, $"{scope}: 数组长度不一致");
                    for (var i = 0; i < expectedList.Count; i++)
                    {
                        AssertSameValue($"{scope}[{i}]", expectedList[i], actualList[i]);
                    }
                    break;
                default:
                    Assert.AreEqual(expected, actual, scope);
                    break;
            }
        }

        private sealed class RawHolder
        {
            public object? Root { get; set; }
        }
    }
}
