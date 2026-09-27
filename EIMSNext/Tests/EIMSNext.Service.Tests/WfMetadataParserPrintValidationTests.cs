using System.Text.Json;

using EIMSNext.Common;
using EIMSNext.Component;
using EIMSNext.Entities;

namespace EIMSNext.Service.Tests
{
    [TestClass]
    public class WfMetadataParserPrintValidationTests
    {
        [TestMethod]
        public void Parse_PrintNodeWithoutSource_ReturnsBadRequest()
        {
            var parser = new WfMetadataParser();
            var definition = new Wf_Definition
            {
                CorpId = "corp-print-validation",
                ExternalId = "eventFlow-invalid-print",
                Version = 1,
                FlowType = FlowType.EventFlow,
                Content = new
                {
                    StartNode = new
                    {
                        Id = "start",
                        Name = "start",
                        NodeType = WfNodeType.Start,
                        NextId = "print",
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
                    Nodes = new[]
                    {
                        new
                        {
                            Id = "print",
                            Name = "print",
                            NodeType = WfNodeType.Print,
                            PrevId = "start",
                            NextId = "end",
                            Metadata = new
                            {
                                PrintMeta = new
                                {
                                    FormId = "source-form",
                                    PrintDefId = "print-definition",
                                    SingleResult = true,
                                }
                            }
                        }
                    },
                    EndNode = new
                    {
                        Id = "end",
                        Name = "end",
                        NodeType = WfNodeType.End,
                        PrevId = "print",
                        Metadata = new { }
                    }
                }.SerializeToJson()
            };

            var error = Assert.ThrowsExactly<BadRequestException>(() => parser.Parse(definition));

            StringAssert.Contains(error.Message, "打印来源节点不存在");
        }
    }
}
