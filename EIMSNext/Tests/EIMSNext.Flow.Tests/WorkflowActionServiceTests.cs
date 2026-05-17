using EIMSNext.Flow.Core;
using EIMSNext.Service.Entities;

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
    }
}
