using System.Text.Json;

using EIMSNext.Component;
using EIMSNext.Entities;

namespace EIMSNext.Service.Tests
{
    [TestClass]
    public class FormRelatedSourceResolverTests
    {
        [TestMethod]
        public void ResolveFormIds_CollectsOnlySupportedRemoteOptionSourcesRecursively()
        {
            const string layout = """
            [
              {
                "type": "dataselect",
                "field": "selected",
                "props": { "dataSource": "form-data-select" }
              },
              {
                "type": "tableform",
                "field": "lines",
                "props": {
                  "columns": [
                    {
                      "rule": [
                        {
                          "type": "radio",
                          "field": "remoteRadio",
                          "effect": {
                            "source": {
                              "label": { "formId": "form-options", "field": "name" },
                              "value": { "formId": "form-options", "field": "code" }
                            }
                          }
                        }
                      ]
                    }
                  ]
                }
              },
              {
                "type": "select",
                "field": "remoteSingleSelect",
                "effect": {
                  "source": {
                    "formId": "form-select1",
                    "label": { "field": "name" },
                    "value": { "field": "code" }
                  }
                }
              },
              {
                "type": "select2",
                "field": "remoteSelect",
                "effect": {
                  "source": {
                    "formId": "form-select2",
                    "label": { "field": "name" },
                    "value": { "field": "id" }
                  }
                }
              },
              {
                "type": "checkbox",
                "field": "remoteCheckbox",
                "effect": {
                  "source": { "formId": "form-checkbox" }
                }
              }
            ]
            """;

            var result = FormRelatedSourceResolver.ResolveFormIds(layout);

            CollectionAssert.AreEquivalent(
                new[] { "form-data-select", "form-select1", "form-select2" },
                result.ToArray());
        }

        [TestMethod]
        public void ResolveFormIds_IgnoresStaticOptionsAndMalformedLayout()
        {
            const string staticLayout = """
            [{"type":"checkbox","field":"tags","options":[{"label":"A","value":"a"}]}]
            """;

            Assert.AreEqual(0, FormRelatedSourceResolver.ResolveFormIds(staticLayout).Count);
            Assert.AreEqual(0, FormRelatedSourceResolver.ResolveFormIds("not-json").Count);
        }

        [TestMethod]
        public void FormDefSerialization_DoesNotExposePublicRelatedFormIds()
        {
            var form = new FormDef
            {
                Id = "form-a",
                PublicRelatedFormIds = ["form-b"],
            };

            var json = JsonSerializer.Serialize(form);

            Assert.IsFalse(json.Contains("PublicRelatedFormIds", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(json.Contains("form-b", StringComparison.Ordinal));
        }
    }
}
