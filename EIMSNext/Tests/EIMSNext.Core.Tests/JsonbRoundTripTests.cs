using System.Dynamic;
using System.Globalization;

using EIMSNext.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EIMSNext.Core.Tests
{
    /// <summary>
    /// jsonb 列「带类型往返」深度验证：
    /// <list type="bullet">
    /// <item><description><c>FormData.Data</c>（<see cref="Dictionary{String,Object}"/>）落 jsonb，查询回来后内部值是否仍是「有类型」的 CLR 值
    /// （long / decimal / string / bool / 嵌套 Dictionary / List&lt;object?&gt;），而不是退化为 <see cref="System.Text.Json.JsonElement"/>。
    /// 公式引擎只接收 CLR 标量/字典/列表，若内部是 JsonElement，数值算术、布尔判断都会失效。</description></item>
    /// <item><description><c>FormDef.Content</c>（<see cref="FormContent"/> 强类型）落 jsonb，查询回来后字段类型是否保持正确。</description></item>
    /// </list>
    /// </summary>
    [TestClass]
    public class JsonbRoundTripTests : TestBase
    {
        [TestMethod]
        public void FormData_Data_RoundTripsWithClrTypes()
        {
            // 构造一个覆盖各种 CLR 类型的 Dictionary（与公式引擎真实会拿到的结构一致）。
            var data = new Dictionary<string, object?>();
            data["intField"] = 42;                       // int
            data["longField"] = 9000000000L;             // long
            data["decimalField"] = 12.34m;               // decimal
            data["doubleField"] = 3.14159d;              // double
            data["stringField"] = "你好world";            // string
            data["boolField"] = true;                    // bool
            data["nullField"] = null;                    // null

            var nestedData = new Dictionary<string, object?>();
            nestedData["a"] = 1;
            nestedData["b"] = "two";
            nestedData["c"] = false;
            data["nested"] = nestedData;

            var obj1 = new Dictionary<string, object?>();
            obj1["id"] = "x1";
            obj1["v"] = 10.5m;   // 带小数位才能在 JSON 往返后保持 decimal（10m 会被序列化成 10，读回是 long）
            var obj2 = new Dictionary<string, object?>();
            obj2["id"] = "x2";
            obj2["v"] = 20.5m;
            data["arrayOfObjects"] = new List<object?> { obj1, obj2 };

            data["arrayOfInts"] = new List<object?> { 1, 2, 3 };
            data["dateField"] = new DateTime(2026, 9, 19, 12, 0, 0, DateTimeKind.Utc);

            var entity = new FormData
            {
                Id = "RT_FD_1",
                CorpId = "C1",
                AppId = "A1",
                FormId = "F1",
                Data = data,
            };

            _dbContext!.TestFormDatas.Add(entity);
            _dbContext.SaveChanges();

            // AsNoTracking 强制真正从数据库重新物化——否则 EF 的 identity resolution
            // 会直接返回跟踪中的种子实例，测出来的是内存对象而不是往返结果。
            var reloaded = _dbContext.TestFormDatas.IgnoreQueryFilters().AsNoTracking().Single(f => f.Id == "RT_FD_1");
            var back = (IDictionary<string, object?>)reloaded.Data;

            var failures = new List<string>();

            // 关键断言：任何内部值都不应是 JsonElement——否则公式引擎无法正确运算。
            foreach (var kvp in back)
            {
                if (kvp.Value is System.Text.Json.JsonElement)
                    failures.Add($"{kvp.Key} 退化为 JsonElement（公式引擎无法识别）");
            }

            AssertClrType(back, "intField", typeof(long), failures);          // 整数还原为 long（与 Json 层映射一致）
            AssertClrType(back, "longField", typeof(long), failures);         // 大整数还原为 long
            AssertClrType(back, "decimalField", typeof(decimal), failures);
            AssertNumeric(back, "doubleField", 3.14159d, failures);
            AssertClrType(back, "stringField", typeof(string), failures);
            AssertClrType(back, "boolField", typeof(bool), failures);
            AssertIsNull(back, "nullField", failures);
            // DateTime 经默认序列化是 ISO 字符串，读回为 string（与 Json 层行为一致），
            // 由下游按字段语义自行解析；关键是不能退化成 JsonElement。
            if (back["dateField"] is not string dateStr || !dateStr.Contains("2026-09-19"))
                failures.Add($"dateField 不是预期的 ISO 字符串，实际：{TypeName(back["dateField"])}（{back["dateField"]}）");

            // 嵌套对象：应是 Dictionary / IDictionary，且内部值也有类型。
            if (back["nested"] is not IDictionary<string, object?> nested)
                failures.Add("nested 不是 IDictionary/Dictionary，实际：" + TypeName(back["nested"]));
            else
            {
                AssertInteger(nested, "a", 1, failures);
                AssertClrType(nested, "b", typeof(string), failures);
                AssertClrType(nested, "c", typeof(bool), failures);
            }

            // 对象数组：应是 List<object?>，元素是 Dictionary。
            if (back["arrayOfObjects"] is not IEnumerable<object?> arr)
                failures.Add("arrayOfObjects 不是 IEnumerable，实际：" + TypeName(back["arrayOfObjects"]));
            else
            {
                var items = arr.ToList();
                if (items.Count != 2) failures.Add($"arrayOfObjects 元素数={items.Count}（期望 2）");
                if (items[0] is not IDictionary<string, object?> first)
                    failures.Add("arrayOfObjects[0] 不是 IDictionary");
                else
                    AssertClrType(first, "v", typeof(decimal), failures);
            }

            // 整数数组：元素应是 long。
            if (back["arrayOfInts"] is not IEnumerable<object?> ints)
                failures.Add("arrayOfInts 不是 IEnumerable");
            else
            {
                var elems = ints.ToList();
                if (elems.Count != 3) failures.Add($"arrayOfInts 元素数={elems.Count}");
                if (elems[0] is not int and not long) failures.Add("arrayOfInts[0] 不是整数，实际：" + TypeName(elems[0]));
            }

            if (failures.Count > 0)
                Assert.Fail("FormData.Data jsonb 往返后类型不一致：\n" + string.Join("\n", failures));
        }

        [TestMethod]
        public void FormDef_Content_RoundTripsWithClrTypes()
        {
            var content = new FormContent
            {
                Layout = "vertical",
                Options = "{}",
                Items = new List<EIMSNext.Entities.FieldDef>
                {
                    new EIMSNext.Entities.FieldDef
                    {
                        Field = "amount",
                        Type = "number",
                        Title = "金额",
                        Props = new FieldProp
                        {
                            Required = true,
                            Options = new List<ValueOption>
                            {
                                new ValueOption { Value = "a", Label = "A" },
                                new ValueOption { Value = "b", Label = "B" },
                            },
                            ValueProp = new ValueProp { Formula = "amount * 2", Depends = "amount" },
                        },
                    },
                    new EIMSNext.Entities.FieldDef
                    {
                        Field = "name",
                        Type = "input",
                        Title = "名称",
                        Required = true,
                    },
                },
                FieldChangeLogs = new List<FieldChangeLog>
                {
                    new FieldChangeLog { FieldId = "old1", FieldType = "input", FieldLabel = "旧字段", DeletedTime = 123L },
                },
            };

            var formDef = new FormDef
            {
                Id = "RT_FD2",
                CorpId = "C1",
                AppId = "A1",
                Name = "测试表单",
                Content = content,
            };

            _dbContext!.TestFormDefs.Add(formDef);
            _dbContext.SaveChanges();

            var reloaded = _dbContext.TestFormDefs.IgnoreQueryFilters().AsNoTracking().Single(f => f.Id == "RT_FD2");
            var back = reloaded.Content;

            var failures = new List<string>();

            if (back is null) { Assert.Fail("FormDef.Content 读取为 null"); return; }
            if (back.Layout is not string) failures.Add("Content.Layout 不是 string");
            if (back.Items is null) failures.Add("Content.Items 为 null");
            else
            {
                if (back.Items.Count != 2) failures.Add($"Content.Items.Count={back.Items.Count}（期望 2）");
                var first = back.Items[0];
                if (first.Field is not string) failures.Add("Items[0].Field 不是 string");
                if (first.Type is not string) failures.Add("Items[0].Type 不是 string");
                if (first.Props is null) failures.Add("Items[0].Props 为 null");
                else
                {
                    if (first.Props.Required is not true) failures.Add("Items[0].Props.Required 不是 true");
                    if (first.Props.Options is null) failures.Add("Items[0].Props.Options 为 null");
                    else if (first.Props.Options.Count != 2) failures.Add($"Items[0].Props.Options.Count={first.Props.Options.Count}");
                    else if (first.Props.Options[0].Value is not string) failures.Add("Items[0].Props.Options[0].Value 不是 string");

                    if (first.Props.ValueProp is null) failures.Add("Items[0].Props.ValueProp 为 null");
                    else if (first.Props.ValueProp.Formula is not string) failures.Add("Items[0].Props.ValueProp.Formula 不是 string");
                }
            }

            if (back.FieldChangeLogs is null) failures.Add("Content.FieldChangeLogs 为 null");
            else if (back.FieldChangeLogs.Count != 1) failures.Add($"Content.FieldChangeLogs.Count={back.FieldChangeLogs.Count}");
            else if (back.FieldChangeLogs[0].FieldId is not string) failures.Add("FieldChangeLogs[0].FieldId 不是 string");

            if (failures.Count > 0)
                Assert.Fail("FormDef.Content jsonb 往返后类型不一致：\n" + string.Join("\n", failures));
        }

        // ---- 断言辅助 ----

        private static void AssertClrType(IDictionary<string, object?> dict, string key, Type expected, List<string> failures)
        {
            if (!dict.TryGetValue(key, out var value) || value is null)
            {
                failures.Add($"{key} 缺失或为 null");
                return;
            }
            if (value.GetType() != expected)
                failures.Add($"{key} 类型={TypeName(value)}，期望 {expected.Name}（值={value}）");
        }

        /// <summary>整数可能是 int 或 long（取决于数值大小），两者对公式引擎都是可用数值类型。</summary>
        private static void AssertInteger(IDictionary<string, object?> dict, string key, long expected, List<string> failures)
        {
            if (!dict.TryGetValue(key, out var value) || value is null)
            {
                failures.Add($"{key} 缺失或为 null");
                return;
            }
            if (value is not int and not long)
            {
                failures.Add($"{key} 不是整数类型，实际 {TypeName(value)}（{value}）");
                return;
            }
            if (Convert.ToInt64(value, CultureInfo.InvariantCulture) != expected)
                failures.Add($"{key} 值={value} 与期望 {expected} 不符");
        }

        private static void AssertNumeric(IDictionary<string, object?> dict, string key, double expected, List<string> failures)
        {
            if (!dict.TryGetValue(key, out var value) || value is null)
            {
                failures.Add($"{key} 缺失或为 null");
                return;
            }
            if (value is not IConvertible)
            {
                failures.Add($"{key} 不是数值类型，实际 {TypeName(value)}");
                return;
            }
            var d = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            if (Math.Abs(d - expected) > 1e-9)
                failures.Add($"{key} 值={d} 与期望 {expected} 不符");
        }

        private static void AssertIsNull(IDictionary<string, object?> dict, string key, List<string> failures)
        {
            if (dict.TryGetValue(key, out var value) && value is not null)
                failures.Add($"{key} 应为 null，实际 {TypeName(value)}（{value}）");
        }

        private static string TypeName(object? value) => value?.GetType().FullName ?? "null";
    }
}
