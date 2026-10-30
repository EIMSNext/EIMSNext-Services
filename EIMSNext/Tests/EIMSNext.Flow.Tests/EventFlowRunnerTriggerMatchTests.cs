using System.Reflection;
using EIMSNext.Core.Abstractions;
using EIMSNext.Flow.Core.Interfaces;
using EIMSNext.Flow.Core.Nodes;
using EIMSNext.Entities;

namespace EIMSNext.Flow.Tests
{
    [TestClass]
    public class EventFlowRunnerTriggerMatchTests
    {
        [TestMethod]
        public void FlowType_EventFlow_ShouldKeepPersistedValueOne()
        {
            var value = (int)Enum.Parse<FlowType>(nameof(FlowType.EventFlow));

            Assert.AreEqual(1, value);
        }

#pragma warning disable MSTEST0032 // 故意：枚举数值即持久化契约（与前端 FlowData.ts 对齐），值恒定属预期，作为回归锁保留
        [TestMethod]
        public void CascadeMode_ShouldKeepPersistedValues_AlignedWithFrontend()
        {
            // 数值契约：与前端 FlowData.ts CascadeMode 一一对应，content JSON 原样往返无映射，禁止改动。
            // 三值设计（无 NotSet）：All=0 兼容存量 content（缺省/0 的定义保持全触发），Specified=1，Never=2
            Assert.AreEqual(0, (int)CascadeMode.All);
            Assert.AreEqual(1, (int)CascadeMode.Specified);
            Assert.AreEqual(2, (int)CascadeMode.Never);
        }
#pragma warning restore MSTEST0032

        [TestMethod]
        public void EvaluateCascadeGate_ShouldBlockNever_AndSpecifiedWithEmptyEvents()
        {
            var never = InvokeEvaluateCascadeGate(new EventSetting { CascadeMode = CascadeMode.Never });
            Assert.AreEqual("ChainCascadeNever", never.BlockReason);
            Assert.IsNull(never.AllowedIds);

            var specifiedEmpty = InvokeEvaluateCascadeGate(new EventSetting { CascadeMode = CascadeMode.Specified });
            Assert.AreEqual("ChainCascadeSpecifiedEmpty", specifiedEmpty.BlockReason);
            Assert.IsNull(specifiedEmpty.AllowedIds);
        }

        [TestMethod]
        public void EvaluateCascadeGate_ShouldAllowSpecifiedList_AndPassAllOrDefault()
        {
            var specified = InvokeEvaluateCascadeGate(new EventSetting { CascadeMode = CascadeMode.Specified, SpecifiedEvents = "ef-1, ef-2" });
            Assert.IsNull(specified.BlockReason);
            Assert.IsNotNull(specified.AllowedIds);
            Assert.IsTrue(specified.AllowedIds!.Contains("EF-2"));

            var all = InvokeEvaluateCascadeGate(new EventSetting { CascadeMode = CascadeMode.All });
            Assert.IsNull(all.BlockReason);
            Assert.IsNull(all.AllowedIds);

            // 无 EventSetting 或缺省：与全链「缺省即 All」一致，不设门禁
            var none = InvokeEvaluateCascadeGate(null);
            Assert.IsNull(none.BlockReason);
            Assert.IsNull(none.AllowedIds);
        }

        [TestMethod]
        public void EvaluateChainBlock_ShouldSuppressLoop_WhenFlowIdAlreadyInPath()
        {
            var chain = new EfChainContext { ChainId = "chain-1", Depth = 2, Path = { "ef-a", "ef-b" } };

            var (reason, depthExceeded) = InvokeEvaluateChainBlock(chain, "ef-a", 10);

            Assert.IsNotNull(reason);
            Assert.IsTrue(reason!.StartsWith("ChainLoopSuppressed", StringComparison.Ordinal));
            Assert.IsFalse(depthExceeded);
        }

        [TestMethod]
        public void EvaluateChainBlock_ShouldMatchPath_CaseInsensitive()
        {
            var chain = new EfChainContext { ChainId = "chain-1", Depth = 1, Path = { "EF-A" } };

            var (reason, _) = InvokeEvaluateChainBlock(chain, "ef-a", 10);

            Assert.IsNotNull(reason);
        }

        [TestMethod]
        public void EvaluateChainBlock_ShouldAllowLastLayer_AndBlockBeyond()
        {
            var atLimit = new EfChainContext { ChainId = "chain-1", Depth = 10, Path = { "ef-a" } };
            var (reason, depthExceeded) = InvokeEvaluateChainBlock(atLimit, "ef-b", 10);

            Assert.IsNotNull(reason);
            Assert.IsTrue(reason!.StartsWith("ChainDepthExceeded", StringComparison.Ordinal));
            Assert.IsTrue(depthExceeded);

            var belowLimit = new EfChainContext { ChainId = "chain-1", Depth = 9, Path = { "ef-a" } };
            var (okReason, _) = InvokeEvaluateChainBlock(belowLimit, "ef-b", 10);

            Assert.IsNull(okReason);
        }

        [TestMethod]
        public void EvaluateChainBlock_ShouldPass_WhenNoChain()
        {
            var (reason, depthExceeded) = InvokeEvaluateChainBlock(null, "ef-a", 10);

            Assert.IsNull(reason);
            Assert.IsFalse(depthExceeded);
        }

        [TestMethod]
        public void BuildChainForCandidate_ShouldStartNewChain_WhenParentChainIsNull()
        {
            var chain = InvokeBuildChainForCandidate(null, "ef-a");

            Assert.AreEqual(1, chain.Depth);
            Assert.AreEqual(1, chain.Path.Count);
            Assert.AreEqual("ef-a", chain.Path[0]);
            Assert.IsFalse(string.IsNullOrWhiteSpace(chain.ChainId));
        }

        [TestMethod]
        public void BuildChainForCandidate_ShouldDeriveFromParentChain()
        {
            var parent = new EfChainContext { ChainId = "chain-1", Depth = 1, Path = { "ef-a" } };

            var chain = InvokeBuildChainForCandidate(parent, "ef-b");

            Assert.AreEqual("chain-1", chain.ChainId);
            Assert.AreEqual(2, chain.Depth);
            CollectionAssert.AreEqual(new[] { "ef-a", "ef-b" }, chain.Path.ToArray());
        }

        [TestMethod]
        public void BuildChainForCandidate_ShouldNotPolluteSiblings_FromSameParent()
        {
            // 回归锁：同批候选是兄弟，必须各自从父链派生，不得互相并入 Path/抬升 Depth
            var parent = new EfChainContext { ChainId = "chain-1", Depth = 3, Path = { "ef-root" } };

            var chainA = InvokeBuildChainForCandidate(parent, "ef-a");
            var chainB = InvokeBuildChainForCandidate(parent, "ef-b");

            Assert.AreEqual(4, chainA.Depth);
            Assert.AreEqual(4, chainB.Depth);
            CollectionAssert.AreEqual(new[] { "ef-root", "ef-a" }, chainA.Path.ToArray());
            CollectionAssert.AreEqual(new[] { "ef-root", "ef-b" }, chainB.Path.ToArray());
        }

        [TestMethod]
        public void ChainContext_ShouldRejectMalformedDepthAndPath()
        {
            Assert.IsTrue(new EfChainContext
            {
                ChainId = "chain-1",
                Depth = 2,
                Path = ["ef-a", "ef-b"]
            }.IsValid(10));

            Assert.IsFalse(new EfChainContext
            {
                ChainId = "chain-1",
                Depth = 2,
                Path = ["ef-a"]
            }.IsValid(10));

            Assert.IsFalse(new EfChainContext
            {
                ChainId = "chain-1",
                Depth = 11,
                Path = Enumerable.Repeat("ef-a", 11).ToList()
            }.IsValid(10));
        }

        [TestMethod]
        public void IsEventMatched_ShouldRequireConfiguredNone_WhenIncomingEventIsNone()
        {
            var configuredSubmit = CreateEventFlow(EventType.Submitted);
            var incomingNone = CreateParameter(EventType.None);

            Assert.IsFalse(InvokeIsEventMatched(configuredSubmit, incomingNone));

            var configuredNone = CreateEventFlow(EventType.None);
            Assert.IsTrue(InvokeIsEventMatched(configuredNone, incomingNone));
        }

        [TestMethod]
        public void IsEventMatched_ShouldAllowFlaggedEvent()
        {
            var eventFlow = CreateEventFlow(EventType.Submitted | EventType.Modified);
            var parameter = CreateParameter(EventType.Modified);

            Assert.IsTrue(InvokeIsEventMatched(eventFlow, parameter));
        }

        [TestMethod]
        public void IsChangeFieldsMatched_ShouldRequireIntersection_ForModifiedEvent()
        {
            var eventFlow = CreateEventFlow(EventType.Modified, ["amount", "status"]);

            Assert.IsTrue(InvokeIsChangeFieldsMatched(eventFlow, CreateParameter(EventType.Modified).WithChangeFields(["AMOUNT"])));
            Assert.IsFalse(InvokeIsChangeFieldsMatched(eventFlow, CreateParameter(EventType.Modified).WithChangeFields(["remark"])));
            Assert.IsFalse(InvokeIsChangeFieldsMatched(eventFlow, CreateParameter(EventType.Modified)));
        }

        [TestMethod]
        public void IsChangeFieldsMatched_ShouldIgnoreChangeFields_ForNonModifiedEvent()
        {
            var eventFlow = CreateEventFlow(EventType.Submitted, ["amount"]);
            var parameter = CreateParameter(EventType.Submitted);

            Assert.IsTrue(InvokeIsChangeFieldsMatched(eventFlow, parameter));
        }

        private static Wf_Definition CreateEventFlow(EventType eventType, List<string>? changeFields = null)
        {
            return new Wf_Definition
            {
                FlowType = FlowType.EventFlow,
                SourceId = "form-1",
                EventSetting = new EventSetting { EventType = eventType },
                Metadata = new WfMetadata
                {
                    Steps =
                    [
                        new WfStep
                        {
                            Id = "trigger",
                            EfNodeSetting = new EfNodeSetting
                            {
                                TriggerSetting = new TriggerSetting
                                {
                                    ChangeFields = changeFields
                                }
                            }
                        }
                    ]
                }
            };
        }

        private static EfRunParameter CreateParameter(EventType eventType)
        {
            return new EfRunParameter(
                "user-1",
                "token",
                new FormData { CorpId = "corp-1", FormId = "form-1" },
                EventSourceType.Form,
                eventType,
                string.Empty,
                null);
        }

        private static bool InvokeIsEventMatched(Wf_Definition eventFlow, EfRunParameter parameter)
        {
            var method = typeof(EventFlowRunner).GetMethod("IsEventMatched", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("IsEventMatched method not found");
            return (bool)method.Invoke(null, [eventFlow, parameter])!;
        }

        private static bool InvokeIsChangeFieldsMatched(Wf_Definition eventFlow, EfRunParameter parameter)
        {
            var method = typeof(EventFlowRunner).GetMethod("IsChangeFieldsMatched", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("IsChangeFieldsMatched method not found");
            return (bool)method.Invoke(null, [eventFlow, parameter])!;
        }

        private static (string? BlockReason, HashSet<string>? AllowedIds) InvokeEvaluateCascadeGate(EventSetting? setting)
        {
            var method = typeof(EventFlowRunner).GetMethod("EvaluateCascadeGate", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("EvaluateCascadeGate method not found");
            return ((string? BlockReason, HashSet<string>? AllowedIds))method.Invoke(null, [setting])!;
        }

        private static (string? Reason, bool DepthExceeded) InvokeEvaluateChainBlock(EfChainContext? chain, string eventFlowId, int maxChainDepth)
        {
            var method = typeof(EventFlowRunner).GetMethod("EvaluateChainBlock", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("EvaluateChainBlock method not found");
            return ((string? Reason, bool DepthExceeded))method.Invoke(null, [chain, eventFlowId, maxChainDepth])!;
        }

        private static EfChainContext InvokeBuildChainForCandidate(EfChainContext? parentChain, string eventFlowId)
        {
            var method = typeof(EventFlowRunner).GetMethod("BuildChainForCandidate", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("BuildChainForCandidate method not found");
            return (EfChainContext)method.Invoke(null, [parentChain, eventFlowId])!;
        }
    }
}
