using EIMSNext.Flow.Core;
using EIMSNext.Service.Entities;
using System.Reflection;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Tests
{
    [TestClass]
    public class WorkflowActionServiceTests
    {
        [TestMethod]
        public void BuildFlowPath_ShouldContainAllPreviousNodes_OnLinearFlow()
        {
            var definition = CreateDefinition(
                new WfStep { Id = "start", Name = "发起", NodeType = WfNodeType.Start, NextStepId = "n1" },
                new WfStep { Id = "n1", Name = "一审", NodeType = WfNodeType.Approve, NextStepId = "n2" },
                new WfStep { Id = "n2", Name = "二审", NodeType = WfNodeType.Approve, NextStepId = "end" },
                new WfStep { Id = "end", Name = "结束", NodeType = WfNodeType.End }
            );

            var path = WorkflowActionService.BuildFlowPath(definition, "start", "n2");

            CollectionAssert.AreEquivalent(new[] { "start", "n1", "n2" }, path.ToList());
        }

        [TestMethod]
        public void BuildFlowPath_ShouldFollowBranchPredecessors()
        {
            var definition = CreateDefinition(
                new WfStep { Id = "start", Name = "发起", NodeType = WfNodeType.Start, NextStepId = "branch" },
                new WfStep
                {
                    Id = "branch",
                    Name = "条件",
                    NodeType = WfNodeType.Condition,
                    SelectNextStep = new Dictionary<string, string>
                    {
                        ["n1"] = "cond1",
                        ["n2"] = "cond2",
                    }
                },
                new WfStep { Id = "n1", Name = "一审", NodeType = WfNodeType.Approve, NextStepId = "n3" },
                new WfStep { Id = "n2", Name = "二审", NodeType = WfNodeType.Approve, NextStepId = "n3" },
                new WfStep { Id = "n3", Name = "三审", NodeType = WfNodeType.Approve, NextStepId = "end" },
                new WfStep { Id = "end", Name = "结束", NodeType = WfNodeType.End }
            );

            var path = WorkflowActionService.BuildFlowPath(definition, "start", "n3");

            Assert.IsTrue(path.Contains("start"));
            Assert.IsTrue(path.Contains("n3"));
            Assert.IsTrue(path.Contains("n1") || path.Contains("n2"));
        }

        [TestMethod]
        public void ResetWorkflowPointers_ShouldUseWorkflowLoaderOrder_ForNestedWorkSteps()
        {
            var definition = CreateDefinition(
                new WfStep
                {
                    Id = "container",
                    Name = "容器",
                    NodeType = WfNodeType.Condition,
                    Work =
                    [
                        [
                            new WfStep { Id = "start", Name = "发起", NodeType = WfNodeType.Start },
                            new WfStep { Id = "target", Name = "目标审批", NodeType = WfNodeType.Approve },
                        ]
                    ]
                },
                new WfStep { Id = "end", Name = "结束", NodeType = WfNodeType.End }
            );
            var instance = new WorkflowInstance
            {
                ExecutionPointers =
                [
                    new ExecutionPointer { Id = "old-pointer", StepId = 99, StepName = "旧节点", Active = true }
                ]
            };

            InvokeResetWorkflowPointers(instance, definition, "target");

            Assert.AreEqual(1, instance.ExecutionPointers.Count);
            var pointer = instance.ExecutionPointers.Single();
            Assert.AreEqual(2, pointer.StepId);
            Assert.AreEqual("目标审批", pointer.StepName);
            Assert.AreEqual(PointerStatus.Pending, pointer.Status);
            Assert.IsTrue(pointer.Active);
        }

        [TestMethod]
        public void ResetWorkflowPointers_ShouldDefaultToStartNode()
        {
            var definition = CreateDefinition(
                new WfStep { Id = "start", Name = "发起", NodeType = WfNodeType.Start },
                new WfStep { Id = "approve", Name = "审批", NodeType = WfNodeType.Approve }
            );
            var instance = new WorkflowInstance { ExecutionPointers = [] };

            InvokeResetWorkflowPointers(instance, definition, null);

            var pointer = instance.ExecutionPointers.Single();
            Assert.AreEqual(0, pointer.StepId);
            Assert.AreEqual("发起", pointer.StepName);
        }

        private static Wf_Definition CreateDefinition(params WfStep[] steps)
        {
            return new Wf_Definition
            {
                Metadata = new WfMetadata
                {
                    Steps = steps.ToList()
                }
            };
        }

        private static void InvokeResetWorkflowPointers(WorkflowInstance instance, Wf_Definition definition, string? targetNodeId)
        {
            var method = typeof(WorkflowActionService).GetMethod("ResetWorkflowPointers", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("ResetWorkflowPointers method not found");
            method.Invoke(null, [instance, definition, targetNodeId]);
        }
    }
}
