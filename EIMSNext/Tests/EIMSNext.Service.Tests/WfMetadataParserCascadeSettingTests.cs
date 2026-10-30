using System.Text.Json;

using EIMSNext.Common;
using EIMSNext.Component;
using EIMSNext.Entities;

namespace EIMSNext.Service.Tests
{
    [TestClass]
    public class WfMetadataParserCascadeSettingTests
    {
        private static Wf_Definition BuildDefinition(int efCascade, string[]? eventIds)
        {
            return new Wf_Definition
            {
                CorpId = "corp-cascade-setting",
                ExternalId = "event-flow-cascade",
                Version = 1,
                FlowType = FlowType.EventFlow,
                Content = new
                {
                    StartNode = new
                    {
                        Id = "start",
                        Name = "start",
                        NodeType = WfNodeType.Start,
                        NextId = "end",
                        Metadata = new
                        {
                            TriggerMeta = new
                            {
                                FormId = "source-form",
                                EventType = EventType.Submitted,
                                SingleResult = true,
                            }
                        }
                    },
                    Nodes = Array.Empty<object>(),
                    EndNode = new
                    {
                        Id = "end",
                        Name = "end",
                        NodeType = WfNodeType.End,
                        PrevId = "start",
                        Metadata = new { }
                    },
                    EfCascade = (CascadeMode)efCascade,
                    EventIds = eventIds
                }.SerializeToJson()
            };
        }

        [TestMethod]
        public void Parse_CascadeSpecified_ShouldSyncEventIdsToSpecifiedEvents()
        {
            var parser = new WfMetadataParser();

            var (_, eventSetting) = parser.Parse(BuildDefinition((int)CascadeMode.Specified, new[] { "ef-x", "ef-y" }));

            Assert.AreEqual(CascadeMode.Specified, eventSetting.CascadeMode);
            Assert.AreEqual(",ef-x,ef-y,", eventSetting.SpecifiedEvents);
        }

        [TestMethod]
        public void Parse_CascadeSpecified_WithEmptyEventIds_ShouldClearSpecifiedEvents()
        {
            // 回归锁：UI 删空指定清单后保存，EventSetting.SpecifiedEvents 必须同步清空，
            // 否则后端按旧清单幽灵触发（UI 显示空但行为触发旧名单）
            var parser = new WfMetadataParser();

            var (_, eventSetting) = parser.Parse(BuildDefinition((int)CascadeMode.Specified, Array.Empty<string>()));

            Assert.AreEqual(CascadeMode.Specified, eventSetting.CascadeMode);
            Assert.IsNull(eventSetting.SpecifiedEvents);
        }

        [TestMethod]
        public void Parse_CascadeSpecified_WithNullEventIds_ShouldClearSpecifiedEvents()
        {
            var parser = new WfMetadataParser();

            var (_, eventSetting) = parser.Parse(BuildDefinition((int)CascadeMode.Specified, null));

            Assert.AreEqual(CascadeMode.Specified, eventSetting.CascadeMode);
            Assert.IsNull(eventSetting.SpecifiedEvents);
        }

        [TestMethod]
        public void Parse_CascadeNever_ShouldNeverBlockAndHaveNoSpecifiedEvents()
        {
            var parser = new WfMetadataParser();

            var (_, eventSetting) = parser.Parse(BuildDefinition((int)CascadeMode.Never, null));

            Assert.AreEqual(CascadeMode.Never, eventSetting.CascadeMode);
            Assert.IsNull(eventSetting.SpecifiedEvents);
        }

        [TestMethod]
        public void Parse_CascadeAll_WithEventIds_ShouldKeepSpecifiedEventsSynced()
        {
            // All 模式运行期不读 SpecifiedEvents，但保持同步：用户切回 Specified 时 UI 回显与后端一致
            var parser = new WfMetadataParser();

            var (_, eventSetting) = parser.Parse(BuildDefinition((int)CascadeMode.All, new[] { "ef-x" }));

            Assert.AreEqual(CascadeMode.All, eventSetting.CascadeMode);
            Assert.AreEqual(",ef-x,", eventSetting.SpecifiedEvents);
        }
    }
}
