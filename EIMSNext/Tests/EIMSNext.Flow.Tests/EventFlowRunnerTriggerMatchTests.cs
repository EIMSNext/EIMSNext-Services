using System.Reflection;
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
                null,
                CascadeMode.NotSet,
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
    }
}
