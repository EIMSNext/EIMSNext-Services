using System.Dynamic;
using System.Text.Json;
using System.Collections;

using EIMSNext.ApiCore.Plugin;
using EIMSNext.Common.Extensions;
using EIMSNext.Plugin.Contracts;
using EIMSNext.Service.Entities;

using HKH.Mef2.Integration;

using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Nodes
{
    public class DfPluginNode : DfNodeBase<DfPluginNode>
    {
        public DfPluginNode(IResolver resolver) : base(resolver)
        {
        }

        public override ExecutionResult Run(IStepExecutionContext context)
        {
            var dataContext = GetDataContext(context);
            var setting = Metadata?.DfNodeSetting?.PluginSetting;
            if (setting == null || string.IsNullOrWhiteSpace(setting.PluginId) || string.IsNullOrWhiteSpace(setting.FunctionId))
            {
                CreateExecLog(context.Workflow, dataContext, Metadata!, "插件节点未配置");
                return ExecutionResult.Next();
            }

            var runtimeManager = Resolver.Resolve<IPluginRuntimeManager>();
            var payload = BuildPayload(dataContext, setting);
            var invocationContext = new PluginInvocationContext
            {
                Resolver = Resolver,
                CorpId = dataContext.CorpId,
                UserId = dataContext.UserId,
                Items = new Dictionary<string, object?>
                {
                    ["workflowId"] = context.Workflow.Id,
                    ["nodeId"] = Metadata?.Id,
                    ["dataId"] = dataContext.DataId,
                }
            };

            var result = runtimeManager.ExecuteAsync(
                    setting.PluginId,
                    setting,
                    new PluginExecArgs { FunName = setting.FunctionId, FunArgs = JsonSerializer.Serialize(payload) },
                    invocationContext,
                    context.CancellationToken)
                .GetAwaiter()
                .GetResult();

            if (result.Code != 0)
            {
                CreateExecLog(context.Workflow, dataContext, Metadata!, result.Message ?? "插件执行失败");
            }
            else
            {
                SavePluginNodeResult(dataContext, result.Result, setting);
                CreateExecLog(context.Workflow, dataContext, Metadata!);
            }

            return ExecutionResult.Next();
        }

        private Dictionary<string, object?> BuildPayload(DfDataContext dataContext, Plugin.Contracts.PluginSetting setting)
        {
            var scriptData = GetNodeScriptData(dataContext);
            var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in setting.FieldSettings)
            {
                payload[field.FieldKey] = ResolveFieldValue(field, scriptData);
            }

            return payload;
        }

        private object? ResolveFieldValue(PluginFieldSetting field, Dictionary<string, object> scriptData)
        {
            return field.ValueType switch
            {
                PluginValueType.Empty => null,
                PluginValueType.Field when field.ValueField != null => ResolveMappedFieldValue(field.ValueField, scriptData),
                _ => field.Value,
            };
        }

        private object? ResolveMappedFieldValue(PluginFieldReference field, Dictionary<string, object> scriptData)
        {
            var value = ScriptEngine.Evaluate(BuildFieldExpression(field), scriptData).Value;
            if (!field.IsSubField)
            {
                return NormalizeComplexValue(field.FieldType, value);
            }

            if (value is not string && value is IEnumerable enumerable)
            {
                var list = new List<object?>();
                foreach (var item in enumerable)
                {
                    list.Add(NormalizeComplexValue(field.FieldType, item));
                }

                return list;
            }

            return NormalizeComplexValue(field.FieldType, value);
        }

        private object? NormalizeComplexValue(string fieldType, object? value)
        {
            if (value == null)
            {
                return null;
            }

            if (string.Equals(fieldType, EIMSNext.Common.FieldType.FileUpload, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fieldType, EIMSNext.Common.FieldType.ImageUpload, StringComparison.OrdinalIgnoreCase))
            {
                return NormalizeUploadValue(value);
            }

            return value;
        }

        private static object? NormalizeUploadValue(object value)
        {
            if (value is string)
            {
                return value;
            }

            if (value is IDictionary<string, object?> dict)
            {
                return new
                {
                    id = dict.TryGetValue("id", out var id) ? id : null,
                    fileName = dict.TryGetValue("fileName", out var fileName) ? fileName : null,
                    savePath = dict.TryGetValue("savePath", out var savePath) ? savePath : null,
                    thumbPath = dict.TryGetValue("thumbPath", out var thumbPath) ? thumbPath : null,
                    fileExt = dict.TryGetValue("fileExt", out var fileExt) ? fileExt : null,
                    fileSize = dict.TryGetValue("fileSize", out var fileSize) ? fileSize : null,
                };
            }

            return value;
        }

        private static string BuildFieldExpression(PluginFieldReference field)
        {
            if (!field.IsSubField)
            {
                return $"data.n_{field.NodeId}.{field.Field}";
            }

            var parts = field.Field.Split('>', 2, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 2
                ? $"MAP(data.n_{field.NodeId}.{parts[0]},'{parts[1]}')"
                : $"data.n_{field.NodeId}.{field.Field}";
        }

        private void SavePluginNodeResult(DfDataContext dataContext, object? pluginResult, Plugin.Contracts.PluginSetting setting)
        {
            var payload = new ExpandoObject();
            payload.AddOrUpdate("result", ToScriptValue(pluginResult));

            var resultMap = ToDictionary(pluginResult);
            foreach (var field in setting.ResultFields)
            {
                resultMap.TryGetValue(field.FieldKey, out var value);
                payload.AddOrUpdate(field.FieldKey, ToScriptValue(value));
            }

            var formData = new FormData
            {
                AppId = dataContext.AppId,
                CorpId = dataContext.CorpId,
                FormId = string.Empty,
                Data = payload,
                CreateBy = dataContext.WfStarter,
                CreateTime = DateTime.UtcNow.ToTimeStampMs(),
            };

            dataContext.NodeDatas[Metadata!.Id] = new DfNodeData
            {
                NodeId = Metadata.Id,
                SingleResult = true,
                NodeExecResult = pluginResult,
                ActionDatas = new List<ActionFormData>
                {
                    new ActionFormData { State = DataState.Unchanged, FormData = formData }
                }
            };
        }

        private static IDictionary<string, object?> ToDictionary(object? value)
        {
            if (value == null)
            {
                return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            }

            if (value is IDictionary<string, object?> dictionary)
            {
                return new Dictionary<string, object?>(dictionary, StringComparer.OrdinalIgnoreCase);
            }

            if (value is IDictionary legacyDictionary)
            {
                var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (DictionaryEntry item in legacyDictionary)
                {
                    var key = item.Key?.ToString();
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        result[key] = item.Value;
                    }
                }

                return result;
            }

            var json = JsonSerializer.Serialize(value);
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(json)
                ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        private static object? ToScriptValue(object? value)
        {
            if (value == null)
            {
                return null;
            }

            if (value is JsonElement jsonElement)
            {
                return ConvertJsonElement(jsonElement);
            }

            if (value is IDictionary<string, object?> dictionary)
            {
                var expando = new ExpandoObject();
                foreach (var item in dictionary)
                {
                    expando.AddOrUpdate(item.Key, ToScriptValue(item.Value));
                }

                return expando;
            }

            if (value is IDictionary legacyDictionary)
            {
                var expando = new ExpandoObject();
                foreach (DictionaryEntry item in legacyDictionary)
                {
                    var key = item.Key?.ToString();
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        expando.AddOrUpdate(key, ToScriptValue(item.Value));
                    }
                }

                return expando;
            }

            if (value is IEnumerable enumerable and not string)
            {
                var list = new List<object?>();
                foreach (var item in enumerable)
                {
                    list.Add(ToScriptValue(item));
                }

                return list;
            }

            return value;
        }

        private static object? ConvertJsonElement(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object => ConvertJsonObject(element),
                JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number when element.TryGetInt64(out var intValue) => intValue,
                JsonValueKind.Number when element.TryGetDecimal(out var decimalValue) => decimalValue,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => element.ToString(),
            };
        }

        private static ExpandoObject ConvertJsonObject(JsonElement element)
        {
            var result = new ExpandoObject();
            foreach (var property in element.EnumerateObject())
            {
                result.AddOrUpdate(property.Name, ConvertJsonElement(property.Value));
            }

            return result;
        }
    }
}
