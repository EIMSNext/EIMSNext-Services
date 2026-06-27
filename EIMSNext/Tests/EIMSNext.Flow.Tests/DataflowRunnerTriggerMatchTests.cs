using System.Reflection;
using EIMSNext.Flow.Core.Interfaces;
using EIMSNext.Flow.Core.Nodes;
using EIMSNext.Service.Entities;

namespace EIMSNext.Flow.Tests
{
    [TestClass]
    public class DataflowRunnerTriggerMatchTests
    {
        [TestMethod]
        public void IsEventMatched_ShouldRequireConfiguredNone_WhenIncomingEventIsNone()
        {
            var configuredSubmit = CreateDataflow(EventType.Submitted);
            var incomingNone = CreateParameter(EventType.None);

            Assert.IsFalse(InvokeIsEventMatched(configuredSubmit, incomingNone));

            var configuredNone = CreateDataflow(EventType.None);
            Assert.IsTrue(InvokeIsEventMatched(configuredNone, incomingNone));
        }

        [TestMethod]
        public void IsEventMatched_ShouldAllowFlaggedEvent()
        {
            var dataflow = CreateDataflow(EventType.Submitted | EventType.Modified);
            var parameter = CreateParameter(EventType.Modified);

            Assert.IsTrue(InvokeIsEventMatched(dataflow, parameter));
        }

        [TestMethod]
        public void IsChangeFieldsMatched_ShouldRequireIntersection_ForModifiedEvent()
        {
            var dataflow = CreateDataflow(EventType.Modified, ["amount", "status"]);

            Assert.IsTrue(InvokeIsChangeFieldsMatched(dataflow, CreateParameter(EventType.Modified).WithChangeFields(["AMOUNT"])));
            Assert.IsFalse(InvokeIsChangeFieldsMatched(dataflow, CreateParameter(EventType.Modified).WithChangeFields(["remark"])));
            Assert.IsFalse(InvokeIsChangeFieldsMatched(dataflow, CreateParameter(EventType.Modified)));
        }

        [TestMethod]
        public void IsChangeFieldsMatched_ShouldIgnoreChangeFields_ForNonModifiedEvent()
        {
            var dataflow = CreateDataflow(EventType.Submitted, ["amount"]);
            var parameter = CreateParameter(EventType.Submitted);

            Assert.IsTrue(InvokeIsChangeFieldsMatched(dataflow, parameter));
        }

        private static Wf_Definition CreateDataflow(EventType eventType, List<string>? changeFields = null)
        {
            return new Wf_Definition
            {
                FlowType = FlowType.Dataflow,
                SourceId = "form-1",
                EventSetting = new EventSetting { EventType = eventType },
                Metadata = new WfMetadata
                {
                    Steps =
                    [
                        new WfStep
                        {
                            Id = "trigger",
                            DfNodeSetting = new DfNodeSetting
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

        private static DfRunParamter CreateParameter(EventType eventType)
        {
            return new DfRunParamter(
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

        private static bool InvokeIsEventMatched(Wf_Definition dataflow, DfRunParamter parameter)
        {
            var method = typeof(DataflowRunner).GetMethod("IsEventMatched", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("IsEventMatched method not found");
            return (bool)method.Invoke(null, [dataflow, parameter])!;
        }

        private static bool InvokeIsChangeFieldsMatched(Wf_Definition dataflow, DfRunParamter parameter)
        {
            var method = typeof(DataflowRunner).GetMethod("IsChangeFieldsMatched", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("IsChangeFieldsMatched method not found");
            return (bool)method.Invoke(null, [dataflow, parameter])!;
        }
    }
}
