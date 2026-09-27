using System.Collections;
using System.Globalization;
using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Query;

namespace EIMSNext.Core.Tests
{
    [TestClass]
    public class DynamicFilterMongoParityTests : TestBase
    {
        private List<FormData> Seed()
        {
            var resp = new FormDataRepository(_dbContext!);
            var rows = new List<FormData>
            {
                Make("R1", "{\"f_name\":\"aaa\",\"f_num\":10,\"f_tags\":[\"x\",\"y\"]}", "C1"),
                Make("R2", "{\"f_name\":\"bbb\",\"f_num\":20,\"f_tags\":[\"y\",\"z\"]}", "C2"),
                Make("R3", "{\"f_name\":\"ccc\",\"f_tags\":[\"z\"]}", null),          // 缺 f_num
                Make("R4", "{\"f_name\":\"ddd\",\"f_num\":10,\"f_tags\":[]}", "C1"),  // f_tags 空数组
                Make("R5", "{\"f_name\":\"eee\",\"f_num\":null,\"f_tags\":[\"x\"]}", null), // f_num 显式 null
                Make("R6", "{\"f_name\":\"fff\",\"f_status\":\"active\",\"f_tags\":[\"z\",\"x\"]}", "C2"), // 缺 f_num
            };
            resp.Insert(rows);
            return rows;
        }

        private static FormData Make(string id, string json, string? corpId)
        {
            return new FormData(json) { Id = id, CorpId = corpId };
        }

        private HashSet<string> PgIds(DynamicFilter filter)
        {
            var resp = new FormDataRepository(_dbContext!);
            return resp.Find(new DynamicFindOptions<FormData> { Filter = filter })
                .ToList().Select(x => x.Id).ToHashSet();
        }

        private static HashSet<string> MongoIds(IEnumerable<FormData> all, DynamicFilter filter)
            => all.Where(d => MongoReference.Matches(d, filter)).Select(x => x.Id).ToHashSet();

        private static DynamicFilter F(string field, string op, object? value = null, string? type = null)
            => new DynamicFilter { Field = field, Op = op, Value = value, Type = type };

        [TestMethod]
        public void JsonbOperators_AreConsistentWithMongo()
        {
            var all = Seed();
            var cases = new (string name, DynamicFilter filter)[]
            {
                ("eq_present",        F("data.f_name", FilterOp.Eq, "aaa")),
                ("eq_missing_key",    F("data.f_num", FilterOp.Eq, 10)),
                ("ne_missing_key",    F("data.f_num", FilterOp.Ne, 10)),
                ("ne_present_null",   F("data.f_num", FilterOp.Ne, 10)),
                ("in_array",          F("data.f_tags", FilterOp.In, new List<object?> { "x" })),
                ("nin_array",         F("data.f_tags", FilterOp.Nin, new List<object?> { "x" })),
                ("gt",                F("data.f_num", FilterOp.Gt, 15)),
                ("gte",               F("data.f_num", FilterOp.Gte, 20)),
                ("lt",                F("data.f_num", FilterOp.Lt, 20)),
                ("lte",               F("data.f_num", FilterOp.Lte, 10)),
                ("between",           F("data.f_num", FilterOp.Between, new List<object?> { 10, 20 })),
                ("empty_missing",     F("data.f_num", FilterOp.Empty)),
                ("empty_present_null",F("data.f_num", FilterOp.Empty)),
                ("notempty_missing",  F("data.f_num", FilterOp.NotEmpty)),
                ("notempty_present",  F("data.f_num", FilterOp.NotEmpty)),
                ("exists_missing",     F("data.f_num", FilterOp.Exists)),
                ("exists_present",     F("data.f_num", FilterOp.Exists)),
                ("text",              F("data.f_name", FilterOp.Text, "a")),
                ("and_group",         new DynamicFilter { Rel = FilterRel.And, Items = new List<DynamicFilter>
                    { F("data.f_name", FilterOp.Eq, "aaa"), F("data.f_num", FilterOp.Gte, 10) } }),
                ("or_group",          new DynamicFilter { Rel = FilterRel.Or, Items = new List<DynamicFilter>
                    { F("data.f_num", FilterOp.Eq, 10), F("data.f_status", FilterOp.Eq, "active") } }),
                ("not_group",         new DynamicFilter { Rel = FilterRel.Not, Items = new List<DynamicFilter>
                    { F("data.f_name", FilterOp.Eq, "aaa") } }),
            };

            var failures = new List<string>();
            foreach (var c in cases)
            {
                var pg = PgIds(c.filter);
                var mg = MongoIds(all, c.filter);
                if (!pg.SetEquals(mg))
                {
                    failures.Add($"{c.name}: pg={{{string.Join(",", pg.OrderBy(x => x))}}} mongo={{{string.Join(",", mg.OrderBy(x => x))}}}");
                }
            }

            Assert.IsTrue(failures.Count == 0,
                "jsonb 运算符逻辑不一致（PG vs Mongo）：\n" + string.Join("\n", failures));
        }

        [TestMethod]
        public void ScalarNullNeNin_AreConsistentWithMongo()
        {
            var all = Seed();

            var neFilter = F("corpId", FilterOp.Ne, "C1");
            var ninFilter = F("corpId", FilterOp.Nin, new List<object?> { "C1" });

            var pgNe = PgIds(neFilter);
            var mgNe = MongoIds(all, neFilter);
            var pgNin = PgIds(ninFilter);
            var mgNin = MongoIds(all, ninFilter);

            // 期望：两者都命中 R2(C2)、R3(NULL)、R5(NULL)、R6(C2)，都不命中 R1/R4(C1)。
            var expected = new[] { "R2", "R3", "R5", "R6" };
            CollectionAssert.AreEquivalent(expected, pgNe.OrderBy(x => x).ToList());
            CollectionAssert.AreEquivalent(expected, mgNe.OrderBy(x => x).ToList());
            CollectionAssert.AreEquivalent(expected, pgNin.OrderBy(x => x).ToList());
            CollectionAssert.AreEquivalent(expected, mgNin.OrderBy(x => x).ToList());

            Assert.IsTrue(pgNe.SetEquals(mgNe) && pgNin.SetEquals(mgNin),
                "scalar-null ne/nin 在 PG 与 Mongo 之间不一致：\n" +
                $"pgNe={{{string.Join(",", pgNe.OrderBy(x => x))}}} mgNe={{{string.Join(",", mgNe.OrderBy(x => x))}}} " +
                $"pgNin={{{string.Join(",", pgNin.OrderBy(x => x))}}} mgNin={{{string.Join(",", mgNin.OrderBy(x => x))}}}");
        }
    }

    /// <summary>
    /// <list type="bullet">
    /// <item><description>非系统字段一律前置 <c>Exists(field,true)</c>（缺失键直接排除）；</description></item>
    /// <item><description>系统字段（id/formId/corpId/createBy/...）不加 Exists，因此 ne/nin 会命中 null；</description></item>
    /// <item><description>empty = <c>Exists(false) OR Eq(null)</c>，即「键缺失 或 值为 null」；</description></item>
    /// </list>
    /// </summary>
    internal static class MongoReference
    {
        public static bool Matches(FormData doc, DynamicFilter filter)
        {
            if (filter == null) return true;
            if (filter.IsEmpty && !filter.IsGroup) return true;

            if (filter.IsGroup)
            {
                var sub = (filter.Items ?? new List<DynamicFilter>())
                    .Select(i => Matches(doc, i)).ToList();
                if (sub.Count == 0) return true;

                return filter.Rel switch
                {
                    FilterRel.Or => sub.Any(x => x),
                    FilterRel.Not => !sub.Aggregate((a, b) => a && b),
                    _ => sub.Aggregate((a, b) => a && b),
                };
            }

            if (string.IsNullOrEmpty(filter.Field) || string.IsNullOrEmpty(filter.Op))
                return true;

            var field = DynamicField.FormatFieldForFilter(filter.Field, filter.Type);
            var op = filter.Op.ToLowerInvariant();
            var values = Normalize(filter.Value);

            var (value, exists) = Resolve(doc, field);
            var isSystem = Fields.IsSystemField(field);
            var present = isSystem || exists;

            if (!present && op != FilterOp.Empty)
                return false;

            return op switch
            {
                FilterOp.Empty => !exists || IsNull(value),
                FilterOp.NotEmpty => exists && !IsNull(value),
                FilterOp.Exists => exists,
                FilterOp.Eq => JsonEquals(value, values.Count > 0 ? values[0] : null),
                FilterOp.Ne => !JsonEquals(value, values.Count > 0 ? values[0] : null),
                FilterOp.In => AsArray(value).Any(v => values.Any(vv => JsonEquals(v, vv))),
                FilterOp.Nin => !AsArray(value).Any(v => values.Any(vv => JsonEquals(v, vv))),
                FilterOp.Gt => Compare(value, values[0]) > 0,
                FilterOp.Gte => Compare(value, values[0]) >= 0,
                FilterOp.Lt => Compare(value, values[0]) < 0,
                FilterOp.Lte => Compare(value, values[0]) <= 0,
                FilterOp.Between => values.Count >= 2
                    ? Compare(value, values[0]) >= 0 && Compare(value, values[1]) <= 0
                    : Compare(value, values.Count > 0 ? values[0] : null) >= 0,
                FilterOp.AllIn => values.All(v => JsonEquals(value, v)),
                FilterOp.Text => (value?.ToString() ?? "")
                    .IndexOf(values.Count > 0 ? values[0]?.ToString() ?? "" : "", StringComparison.OrdinalIgnoreCase) >= 0,
                _ => JsonEquals(value, values.Count > 0 ? values[0] : null),
            };
        }

        private static (object? value, bool exists) Resolve(FormData doc, string field)
        {
            if (field.StartsWith("data.", StringComparison.OrdinalIgnoreCase))
            {
                var key = field.Substring(5);
                var dict = (IDictionary<string, object?>)doc.Data;
                return dict.ContainsKey(key) ? (dict[key], true) : (null, false);
            }

            return field switch
            {
                "corpId" => (doc.CorpId, true),
                "formId" => (doc.FormId, true),
                "id" => (doc.Id, true),
                "createBy.value" => (doc.CreateBy?.Value, doc.CreateBy != null),
                "createBy.id" => (doc.CreateBy?.Id, doc.CreateBy != null),
                _ => (null, false),
            };
        }

        private static List<object?> Normalize(object? value)
        {
            var n = DynamicValueNormalizer.Normalize(value);
            if (n is null) return new List<object?>();
            if (n is IEnumerable enumerable and not string and not IDictionary)
                return enumerable.Cast<object?>().Where(x => x is not null).ToList();
            return new List<object?> { n };
        }

        private static bool IsNull(object? v) => v is null;

        private static bool JsonEquals(object? a, object? b)
        {
            if (a is null && b is null) return true;
            if (a is null || b is null) return false;
            if (IsNumeric(a) && IsNumeric(b))
                return Convert.ToDecimal(a, CultureInfo.InvariantCulture) == Convert.ToDecimal(b, CultureInfo.InvariantCulture);
            if (a is bool ba && b is bool bb) return ba == bb;
            if (a is string sa && b is string sb) return sa == sb;
            return a.Equals(b);
        }

        private static bool IsNumeric(object o) => o is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;

        private static int Compare(object? a, object? b)
        {
            if (IsNumeric(a) && IsNumeric(b))
                return Convert.ToDecimal(a!, CultureInfo.InvariantCulture)
                    .CompareTo(Convert.ToDecimal(b!, CultureInfo.InvariantCulture));
            return string.Compare(a?.ToString(), b?.ToString(), StringComparison.Ordinal);
        }

        private static IEnumerable<object?> AsArray(object? value)
        {
            if (value is IEnumerable enumerable and not string)
                return enumerable.Cast<object?>().ToList();
            return value is null ? Array.Empty<object?>() : new[] { value };
        }
    }
}
