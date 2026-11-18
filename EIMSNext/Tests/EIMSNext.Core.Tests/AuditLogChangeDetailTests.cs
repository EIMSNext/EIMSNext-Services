using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Nodes;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services;

using Microsoft.EntityFrameworkCore.Query;

#pragma warning disable EF1001

namespace EIMSNext.Core.Tests
{
    [TestClass]
    public class AuditLogChangeDetailTests
    {
        private static List<AuditLogChange> Parse(string json)
            => JsonSerializer.Deserialize<List<AuditLogChange>>(json)
               ?? throw new InvalidOperationException("Detail 不是合法的变更数组");

        [TestMethod]
        public void T1_ChangedFields_SkipUnchangedAndSystemFields()
        {
            var oldObj = new JsonObject { ["a"] = "1", ["b"] = "2", ["createTime"] = 100L };
            var newObj = new JsonObject { ["a"] = "10", ["b"] = "2", ["createTime"] = 200L };

            var items = Parse(AuditLogChangeBuilder.BuildChanges(oldObj, newObj));

            Assert.AreEqual(1, items.Count);
            Assert.AreEqual("a", items[0].Field);
            Assert.AreEqual("1", items[0].Old?.ToString());
            Assert.AreEqual("10", items[0].New?.ToString());
        }

        [TestMethod]
        public void T2_KeyAddedInNew_ContainsNullOldValue()
        {
            var oldObj = new JsonObject { ["a"] = "1" };
            var newObj = new JsonObject { ["a"] = "1", ["d"] = "new-field" };

            var items = Parse(AuditLogChangeBuilder.BuildChanges(oldObj, newObj));

            Assert.AreEqual(1, items.Count);
            Assert.AreEqual("d", items[0].Field);
            Assert.IsNull(items[0].Old);
            Assert.AreEqual("new-field", items[0].New?.ToString());
        }

        [TestMethod]
        public void T3_KeyRemovedInNew_ContainsNullNewValue()
        {
            var oldObj = new JsonObject { ["a"] = "1", ["c"] = "3" };
            var newObj = new JsonObject { ["a"] = "1" };

            var items = Parse(AuditLogChangeBuilder.BuildChanges(oldObj, newObj));

            Assert.AreEqual(1, items.Count);
            Assert.AreEqual("c", items[0].Field);
            Assert.AreEqual("3", items[0].Old?.ToString());
            Assert.IsNull(items[0].New);
        }

        [TestMethod]
        public void T4_AllFieldsEqual_ReturnsEmptyArray()
        {
            var oldObj = new JsonObject { ["a"] = "1", ["b"] = "2" };
            var newObj = new JsonObject { ["a"] = "1", ["b"] = "2" };

            Assert.AreEqual(AuditLogChangeBuilder.EmptyChanges,
                AuditLogChangeBuilder.BuildChanges(oldObj, newObj));
        }

        [TestMethod]
        public void T5_MultipleChanges_KeepNewObjectPropertyOrder()
        {
            var oldObj = new JsonObject { ["a"] = "1", ["b"] = "2", ["c"] = "3" };
            var newObj = new JsonObject { ["a"] = "1", ["b"] = "20", ["c"] = "30" };

            var items = Parse(AuditLogChangeBuilder.BuildChanges(oldObj, newObj));

            Assert.AreEqual(2, items.Count);
            Assert.AreEqual("b", items[0].Field);
            Assert.AreEqual("c", items[1].Field);
        }

        [TestMethod]
        public void T6_LongValue_IsClamped()
        {
            var longText = new string('x', 400);
            var oldObj = new JsonObject { ["content"] = "before" };
            var newObj = new JsonObject { ["content"] = longText };

            var items = Parse(AuditLogChangeBuilder.BuildChanges(oldObj, newObj));

            Assert.AreEqual(AuditLogChangeBuilder.MaxValueLength + 1,
                items[0].New?.ToString()?.Length);
            Assert.IsTrue(items[0].New?.ToString()?.EndsWith('…'));
        }

        [TestMethod]
        public void T7_ChangeCount_IsCapped()
        {
            var oldObj = new JsonObject();
            var newObj = new JsonObject();
            for (var i = 0; i < AuditLogChangeBuilder.MaxItems + 10; i++)
            {
                newObj[$"f{i}"] = i;
            }

            var items = Parse(AuditLogChangeBuilder.BuildChanges(oldObj, newObj));

            Assert.AreEqual(AuditLogChangeBuilder.MaxItems, items.Count);
        }

        [TestMethod]
        public void T8_InsertSummary_UsesChineseLabelAndNullOld()
        {
            var entity = new TestAuditEntity { Id = "data-1", Name = "采购订单-A" };

            var items = Parse(AuditLogChangeBuilder.BuildSummary(entity, true));

            Assert.AreEqual(1, items.Count);
            Assert.AreEqual("名称", items[0].Label);
            Assert.IsNull(items[0].Old);
            Assert.AreEqual("采购订单-A", items[0].New?.ToString());
        }

        [TestMethod]
        public void T9_SummaryWithoutMainField_FallsBackToDataId()
        {
            var entity = new TestAuditEntity { Id = "data-1" };

            var items = Parse(AuditLogChangeBuilder.BuildSummary(entity, false));

            Assert.AreEqual(1, items.Count);
            Assert.AreEqual("dataId", items[0].Field);
            Assert.AreEqual("data-1", items[0].Old?.ToString());
            Assert.IsNull(items[0].New);
        }

        [TestMethod]
        public void T10_LimitFilter_IsClamped()
        {
            var filter = new string('f', AuditLogChangeBuilder.MaxFilterLength + 100);

            Assert.AreEqual(AuditLogChangeBuilder.MaxFilterLength + 1,
                AuditLogChangeBuilder.LimitFilter(filter).Length);
            Assert.AreEqual(string.Empty, AuditLogChangeBuilder.LimitFilter(null));
        }

        [TestMethod]
        public void T11_FormatForExport_UsesLabelAndArrow()
        {
            var detail = AuditLogChangeBuilder.Serialize(
            [
                new AuditLogChange { Field = "name", Type = "input", Label = "名称", Old = null, New = "采购订单-A" }
            ]);

            Assert.AreEqual("名称: (空) → 采购订单-A", AuditLogChangeBuilder.FormatForExport(detail));
        }

        [TestMethod]
        public void T12_FormatForExport_LegacyTextIsReturnedAsIs()
        {
            const string legacy = "批量更新数据(无旧对象):x => x.Id == \"a\"";

            Assert.AreEqual(legacy, AuditLogChangeBuilder.FormatForExport(legacy));
            Assert.AreEqual("-", AuditLogChangeBuilder.FormatForExport(null));
        }

        [TestMethod]
        public void T13_Setters_ConstantValuesBecomeChanges()
        {
            var json = AuditLogSettersReader.Build<TestAuditEntity>(
                s => s.SetProperty(x => x.Name, "订单-B")
                      .SetProperty(x => x.Enabled, true));

            var items = Parse(json);
            Assert.AreEqual(2, items.Count);
            Assert.AreEqual("name", items[0].Field);
            Assert.AreEqual("名称", items[0].Label);
            Assert.AreEqual("input", items[0].Type);
            Assert.IsNull(items[0].Old);
            Assert.AreEqual("订单-B", items[0].New?.ToString());
            Assert.AreEqual("enabled", items[1].Field);
            Assert.AreEqual("启用", items[1].Label);
            Assert.AreEqual("switch", items[1].Type);
        }

        [TestMethod]
        public void T14_Setters_SkipSystemFields()
        {
            var json = AuditLogSettersReader.Build<TestAuditEntity>(
                s => s.SetProperty(x => x.Name, "订单-B")
                      .SetProperty(x => x.UpdateTime, 1794447388000L)
                      .SetProperty(x => x.DeleteFlag, true)
                      .SetProperty(x => x.CorpId, "corp-1"));

            var items = Parse(json);
            Assert.AreEqual(1, items.Count);
            Assert.AreEqual("name", items[0].Field);
        }

        [TestMethod]
        public void T15_Setters_ClosureVariableIsEvaluated()
        {
            var title = "来自闭包变量";
            var json = AuditLogSettersReader.Build<TestAuditEntity>(
                s => s.SetProperty(x => x.Name, title));

            Assert.AreEqual("来自闭包变量", Parse(json)[0].New?.ToString());
        }

        [TestMethod]
        public void T16_Setters_DependentExpressionKeepsText()
        {
            var json = AuditLogSettersReader.Build<TestAuditEntity>(
                s => s.SetProperty(x => x.Count, x => x.Count + 1));

            var item = Parse(json)[0];
            Assert.AreEqual("count", item.Field);
            Assert.IsTrue((item.New?.ToString() ?? "").Contains("Count"), item.New?.ToString());
            Assert.IsTrue((item.New?.ToString() ?? "").Contains("+"), item.New?.ToString());
        }

        [TestMethod]
        public void T17_Setters_DegradeToEmptyArray()
        {
            Assert.AreEqual("[]", AuditLogSettersReader.Build<TestAuditEntity>(null));
            Assert.AreEqual("[]", AuditLogSettersReader.Build<TestAuditEntity>(
                _ => throw new InvalidOperationException("setters 异常")));
        }

        [TestMethod]
        public void T18_Setters_LimitToMaxItems()
        {
            var json = AuditLogSettersReader.Build<TestAuditEntity>(s =>
            {
                for (var i = 0; i < 60; i++) s.SetProperty(x => x.Name, "v" + i);
            });

            Assert.AreEqual(AuditLogChangeBuilder.MaxItems, Parse(json).Count);
        }

        [TestMethod]
        public void T19_FormatForExport_BatchUsesUnknownOldMarker()
        {
            var detail = AuditLogChangeBuilder.Serialize(
            [
                new AuditLogChange { Field = "name", Type = "input", Label = "名称", Old = null, New = "订单-B" }
            ]);

            Assert.AreEqual("名称: (空) → 订单-B", AuditLogChangeBuilder.FormatForExport(detail));
            Assert.AreEqual("名称: — → 订单-B", AuditLogChangeBuilder.FormatForExport(detail, DbAction.Update));
            Assert.AreEqual("名称: (空) → 订单-B", AuditLogChangeBuilder.FormatForExport(detail, DbAction.Insert));
        }

        [TestMethod]
        public void T20_FormatForExport_UpdateWithOldValueIsNotBatch()
        {
            var detail = AuditLogChangeBuilder.Serialize(
            [
                new AuditLogChange { Field = "name", Type = "input", Label = "名称", Old = "旧值", New = "新值" }
            ]);

            Assert.AreEqual("名称: 旧值 → 新值", AuditLogChangeBuilder.FormatForExport(detail, DbAction.Update));
            Assert.AreEqual("批量操作（无逐字段变更）", AuditLogChangeBuilder.FormatForExport("[]", DbAction.Update));
        }

        [TestMethod]
        public void T21_FormatForExport_TruncationIncludesItemCount()
        {
            var changes = new List<AuditLogChange>();
            for (var i = 0; i < 40; i++)
            {
                changes.Add(new AuditLogChange
                {
                    Field = $"f{i}", Type = "input", Label = $"字段{i}",
                    Old = null, New = new string('x', 120),
                });
            }

            var text = AuditLogChangeBuilder.FormatForExport(
                AuditLogChangeBuilder.Serialize(changes), DbAction.Update);

            const string suffix = "…（共 40 项）";
            Assert.IsTrue(text.EndsWith(suffix), text);
            Assert.AreEqual(AuditLogChangeBuilder.MaxFilterLength + suffix.Length, text.Length);
        }

        [TestMethod]
        public void T22_Normalize_ConvertsMillisecondTimestampWhenFieldIsTime()
        {
            const long ms = 1794447388000L;

            var converted = AuditLogChangeBuilder.Normalize(ms, "lastVisitTime");
            var expectedDate = DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime().ToString("yyyy-MM-dd");
            Assert.IsNotNull(converted);
            Assert.IsInstanceOfType(converted, typeof(string));
            Assert.IsTrue(((string)converted).StartsWith(expectedDate), (string)converted);

            // 无字段名时不猜语义，保持原始数值
            var raw = AuditLogChangeBuilder.Normalize(ms) as JsonNode;
            Assert.IsNotNull(raw);
            Assert.AreEqual("1794447388000", raw.ToJsonString());
        }

        [TestMethod]
        public void T23_FormatFilter_EvaluatesScalarClosure()
        {
            var formId = "FORM-1";
            Expression<Func<TestAuditEntity, bool>> filter = x => x.Id == formId;

            var text = AuditLogFilterFormatter.Format(filter);

            Assert.IsTrue(text.Contains("FORM-1"), text);
            Assert.IsFalse(text.Contains("value("), text);
        }

        [TestMethod]
        public void T24_FormatFilter_EvaluatesCollectionClosure()
        {
            var idList = new List<string> { "ID-1", "ID-2" };
            Expression<Func<TestAuditEntity, bool>> filter = x => idList.Contains(x.Id);

            var text = AuditLogFilterFormatter.Format(filter);

            Assert.IsTrue(text.Contains("\"ID-1\", \"ID-2\""), text);
            Assert.IsFalse(text.Contains("value("), text);
        }

        [TestMethod]
        public void T25_FormatFilter_EvaluatesNestedMemberChain()
        {
            var holder = new Holder();
            Expression<Func<TestAuditEntity, bool>> filter = x => x.Id == holder.Context.CorpId;

            var text = AuditLogFilterFormatter.Format(filter);

            Assert.IsTrue(text.Contains("corp-1"), text);
            Assert.IsFalse(text.Contains("value("), text);
        }

        [TestMethod]
        public void T26_FormatFilter_DynamicFilterIsSerialized()
        {
            var filter = new DynamicFilter { Field = "Id", Op = FilterOp.In, Value = new[] { "A", "B" } };

            var text = AuditLogFilterFormatter.Format(filter);

            Assert.IsTrue(text.Contains("\"field\":\"Id\""), text);
            Assert.IsTrue(text.Contains("\"op\":\"in\""), text);
            Assert.IsTrue(text.Contains("\"A\""), text);
            Assert.IsFalse(text.Contains("EIMSNext.Core.Query.DynamicFilter"), text);
        }

        [TestMethod]
        public void T27_FormatFilter_DegradesWhenEvaluationThrows()
        {
            var holder = new ThrowingHolder();
            Expression<Func<TestAuditEntity, bool>> filter = x => x.Id == holder.CorpId;

            var text = AuditLogFilterFormatter.Format(filter);

            Assert.IsFalse(string.IsNullOrEmpty(text));
            Assert.IsTrue(text.Contains("ThrowingHolder.CorpId"), text);
        }

        [TestMethod]
        public void T28_FormatFilter_CollectionIsCapped()
        {
            var idList = Enumerable.Range(1, 30).Select(x => $"ID-{x}").ToList();
            Expression<Func<TestAuditEntity, bool>> filter = x => idList.Contains(x.Id);

            var text = AuditLogFilterFormatter.Format(filter);

            Assert.IsTrue(text.Contains("…（共 30 项）"), text);
            Assert.IsFalse(text.Contains("ID-30"), text);
        }

        private sealed class Holder
        {
            public Holder Context => this;
            public string CorpId => "corp-1";
        }

        private sealed class ThrowingHolder
        {
            public string CorpId => throw new InvalidOperationException();
        }

        private sealed class TestAuditEntity : CorpEntityBase
        {
            public string? Name { get; set; }
            public int Count { get; set; }
            public bool Enabled { get; set; }
        }
    }
}

#pragma warning restore EF1001
