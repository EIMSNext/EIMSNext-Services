using System.Text.Json;

using EIMSNext.Component;
using EIMSNext.Entities;

namespace EIMSNext.Service.Tests
{
    [TestClass]
    public class WfMetadataParserApprovalTests
    {
        [TestMethod]
        public void Parse_ByLevelApproval_KeepsRuleAndClearsNormalCandidates()
        {
            var parser = new WfMetadataParser();
            var definition = new Wf_Definition
            {
                CorpId = "corp-workflow",
                ExternalId = "form-001",
                Version = 3,
                FlowType = FlowType.Workflow,
                Content = new
                {
                    StartNode = new
                    {
                        Id = "start",
                        Name = "start",
                        NodeType = WfNodeType.Start,
                        NextId = "approve",
                        Metadata = new { }
                    },
                    Nodes = new[]
                    {
                        new
                        {
                            Id = "approve",
                            Name = "approve",
                            NodeType = WfNodeType.Approve,
                            NextId = "end",
                            Metadata = new
                            {
                                ApproveMeta = new
                                {
                                    ApproverType = ApproverType.ByLevel,
                                    ApproveMode = WfApprovalMode.CounterSign,
                                    ApprovalCandidates = new[]
                                    {
                                        new ApprovalCandidate
                                        {
                                            CandidateType = CandidateType.Employee,
                                            CandidateId = "emp-001",
                                            CandidateName = "Should be cleared"
                                        }
                                    },
                                    ByLevelApprovalSetting = new ByLevelApprovalSetting
                                    {
                                        Terminal = ByLevelApprovalTerminal.Organization,
                                        StartLevel = 1,
                                        EndLevel = 3
                                    }
                                }
                            }
                        }
                    },
                    EndNode = new
                    {
                        Id = "end",
                        Name = "end",
                        NodeType = WfNodeType.End,
                        Metadata = new { }
                    }
                }.SerializeToJson()
            };

            var (metadata, _) = parser.Parse(definition);

            var approveSetting = metadata.Steps.Single(x => x.Id == "approve").WfNodeSetting!.ApproveSetting!;
            Assert.AreEqual(ApproverType.ByLevel, approveSetting.ApproverType);
            Assert.AreEqual(WfApprovalMode.CounterSign, approveSetting.ApprovalMode);
            var candidates = approveSetting.Candidates ?? throw new AssertFailedException("Candidates should be initialized");
            Assert.AreEqual(0, candidates.Count);
            var byLevelSetting = approveSetting.ByLevelApprovalSetting ?? throw new AssertFailedException("ByLevelApprovalSetting should be preserved");
            Assert.AreEqual(ByLevelApprovalTerminal.Organization, byLevelSetting.Terminal);
            Assert.AreEqual(1, byLevelSetting.StartLevel);
            Assert.AreEqual(3, byLevelSetting.EndLevel);
        }

        [TestMethod]
        public void Parse_ReturnAction_KeepsReturnTargetSetting()
        {
            var parser = new WfMetadataParser();
            var definition = new Wf_Definition
            {
                CorpId = "corp-workflow",
                ExternalId = "form-001",
                Version = 3,
                FlowType = FlowType.Workflow,
                Content = new
                {
                    StartNode = new
                    {
                        Id = "start",
                        Name = "start",
                        NodeType = WfNodeType.Start,
                        NextId = "approve-1",
                        Metadata = new { }
                    },
                    Nodes = new object[]
                    {
                        new
                        {
                            Id = "approve-1",
                            Name = "approve-1",
                            NodeType = WfNodeType.Approve,
                            NextId = "approve-2",
                            Metadata = new { ApproveMeta = new { } }
                        },
                        new
                        {
                            Id = "approve-2",
                            Name = "approve-2",
                            NodeType = WfNodeType.Approve,
                            NextId = "end",
                            Metadata = new
                            {
                                ApproveMeta = new
                                {
                                    NodeActions = new[]
                                    {
                                        new
                                        {
                                            ActionType = "return",
                                            Enabled = true,
                                            ReturnSetting = new ReturnSetting
                                            {
                                                TargetMode = ReturnTargetMode.Specified,
                                                TargetNodeId = "approve-1",
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                    EndNode = new
                    {
                        Id = "end",
                        Name = "end",
                        NodeType = WfNodeType.End,
                        Metadata = new { }
                    }
                }.SerializeToJson()
            };

            var (metadata, _) = parser.Parse(definition);

            var returnAction = metadata.Steps.Single(x => x.Id == "approve-2")
                .WfNodeSetting!.ApproveSetting!.NodeActions!
                .Single(x => x.ActionType == NodeActionType.Return);
            Assert.AreEqual(ReturnTargetMode.Specified, returnAction.ReturnSetting!.TargetMode);
            Assert.AreEqual("approve-1", returnAction.ReturnSetting.TargetNodeId);
        }
    }
}
