using System.Text.Json;
using System.Text.Json.Nodes;

using EIMSNext.Service.Entities;

namespace EIMSNext.Service
{
    internal static class AppTemplateReferenceRewriter
    {
        public static string RewriteDashboardLayout(string layout, Dictionary<string, string> layoutMap)
        {
            var node = JsonNode.Parse(string.IsNullOrWhiteSpace(layout) ? "[]" : layout);
            RewriteLayoutNode(node, layoutMap);
            return node?.ToJsonString() ?? "[]";
        }

        public static FormContent RewriteFormContent(FormTemplate formTemplate, string appId, Dictionary<string, string> formMap, Dictionary<string, string> dashboardMap)
        {
            var json = JsonSerializer.Serialize(formTemplate);
            var rewritten = RewriteJsonReferences(json, appId, formMap, dashboardMap, null, null);
            return JsonSerializer.Deserialize<FormTemplateProjection>(rewritten)?.Content ?? new FormContent();
        }

        public static FormSettings RewriteFormSettings(FormTemplate formTemplate, Dictionary<string, string> formMap, Dictionary<string, string> dashboardMap)
        {
            var json = JsonSerializer.Serialize(formTemplate);
            var rewritten = RewriteJsonReferences(json, string.Empty, formMap, dashboardMap, null, null);
            return JsonSerializer.Deserialize<FormTemplateProjection>(rewritten)?.FormSettings ?? new FormSettings();
        }

        public static WfMetadata RewriteWorkflowMetadata(WfMetadata metadata, Dictionary<string, string> formMap, Dictionary<string, string> dashboardMap, Dictionary<string, string> workflowMap, Dictionary<string, string> printMap)
        {
            var json = JsonSerializer.Serialize(metadata);
            var rewritten = RewriteJsonReferences(json, string.Empty, formMap, dashboardMap, workflowMap, printMap);
            return JsonSerializer.Deserialize<WfMetadata>(rewritten) ?? new WfMetadata();
        }

        public static EventSetting? RewriteEventSetting(EventSetting? eventSetting, Dictionary<string, string> formMap, Dictionary<string, string> dashboardMap, Dictionary<string, string> workflowMap)
        {
            if (eventSetting == null)
            {
                return null;
            }

            var json = JsonSerializer.Serialize(eventSetting);
            var rewritten = RewriteJsonReferences(json, string.Empty, formMap, dashboardMap, workflowMap, null);
            return JsonSerializer.Deserialize<EventSetting>(rewritten);
        }

        public static string RewriteJsonReferences(string json, string appId, Dictionary<string, string> formMap, Dictionary<string, string> dashboardMap, Dictionary<string, string>? workflowMap, Dictionary<string, string>? printMap)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return json;
            }

            var node = JsonNode.Parse(json);
            RewriteReferenceNode(node, appId, formMap, dashboardMap, workflowMap, printMap);
            return node?.ToJsonString() ?? json;
        }

        public static string? MapTemplateReference(string? templateId, Dictionary<string, string> formMap, Dictionary<string, string> dashboardMap, Dictionary<string, string>? workflowMap)
        {
            if (string.IsNullOrWhiteSpace(templateId))
            {
                return templateId;
            }

            if (formMap.TryGetValue(templateId, out var formId))
            {
                return formId;
            }

            if (dashboardMap.TryGetValue(templateId, out var dashboardId))
            {
                return dashboardId;
            }

            if (workflowMap != null && workflowMap.TryGetValue(templateId, out var workflowId))
            {
                return workflowId;
            }

            return templateId;
        }

        private static void RewriteLayoutNode(JsonNode? node, Dictionary<string, string> layoutMap)
        {
            switch (node)
            {
                case JsonArray array:
                    foreach (var item in array)
                    {
                        RewriteLayoutNode(item, layoutMap);
                    }
                    break;
                case JsonObject obj:
                    if (obj["i"] is JsonValue idValue)
                    {
                        var oldId = idValue.GetValue<string>();
                        if (!layoutMap.TryGetValue(oldId, out var newId))
                        {
                            newId = Guid.NewGuid().ToString("N");
                            layoutMap[oldId] = newId;
                        }
                        obj["i"] = newId;
                    }
                    foreach (var property in obj.ToList())
                    {
                        RewriteLayoutNode(property.Value, layoutMap);
                    }
                    break;
            }
        }

        private static void RewriteReferenceNode(JsonNode? node, string appId, Dictionary<string, string> formMap, Dictionary<string, string> dashboardMap, Dictionary<string, string>? workflowMap, Dictionary<string, string>? printMap)
        {
            switch (node)
            {
                case JsonArray array:
                    foreach (var item in array)
                    {
                        RewriteReferenceNode(item, appId, formMap, dashboardMap, workflowMap, printMap);
                    }
                    break;
                case JsonObject obj:
                    foreach (var property in obj.ToList())
                    {
                        if (property.Value is JsonValue value && value.TryGetValue<string>(out var text))
                        {
                            if (property.Key.Equals("appId", StringComparison.OrdinalIgnoreCase))
                            {
                                if (!string.IsNullOrWhiteSpace(appId))
                                {
                                    obj[property.Key] = appId;
                                }
                            }
                            else if (property.Key.Equals("formId", StringComparison.OrdinalIgnoreCase)
                                || property.Key.Equals("sourceFormId", StringComparison.OrdinalIgnoreCase)
                                || property.Key.Equals("externalId", StringComparison.OrdinalIgnoreCase))
                            {
                                if (formMap.TryGetValue(text, out var newFormId))
                                {
                                    obj[property.Key] = newFormId;
                                }
                            }
                            else if (property.Key.Equals("sourceId", StringComparison.OrdinalIgnoreCase))
                            {
                                var mappedSourceId = MapTemplateReference(text, formMap, dashboardMap, workflowMap);
                                if (!string.IsNullOrWhiteSpace(mappedSourceId))
                                {
                                    obj[property.Key] = mappedSourceId;
                                }
                            }
                            else if ((property.Key.Equals("dashboardId", StringComparison.OrdinalIgnoreCase) || property.Key.Equals("dashId", StringComparison.OrdinalIgnoreCase)) && dashboardMap.TryGetValue(text, out var newDashboardId))
                            {
                                obj[property.Key] = newDashboardId;
                            }
                            else if ((property.Key.Equals("workflowId", StringComparison.OrdinalIgnoreCase) || property.Key.Equals("dataflowId", StringComparison.OrdinalIgnoreCase))
                                && workflowMap != null
                                && workflowMap.TryGetValue(text, out var newWorkflowId))
                            {
                                obj[property.Key] = newWorkflowId;
                            }
                            else if (property.Key.Equals("printId", StringComparison.OrdinalIgnoreCase)
                                && printMap != null
                                && printMap.TryGetValue(text, out var newPrintId))
                            {
                                obj[property.Key] = newPrintId;
                            }
                        }

                        RewriteReferenceNode(property.Value, appId, formMap, dashboardMap, workflowMap, printMap);
                    }
                    break;
            }
        }

        private sealed class FormTemplateProjection
        {
            public FormContent Content { get; set; } = new FormContent();

            public FormSettings FormSettings { get; set; } = new FormSettings();
        }
    }
}
