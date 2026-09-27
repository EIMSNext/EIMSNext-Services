using System.Dynamic;
using System.Text.Json;
using System.Collections;

using EIMSNext.Plugin.Runtime;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Plugin.Contracts;
using EIMSNext.Entities;

using HKH.Mef2.Integration;

using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Nodes
{
    public class EfPluginNode : EfNodeBase<EfPluginNode>
    {
        public EfPluginNode(IResolver resolver) : base(resolver)
        {
        }

        public override ExecutionResult Run(IStepExecutionContext context)
        {
            var dataContext = GetDataContext(context);
            var startTime = DateTime.UtcNow.ToTimeStampMs();
            var setting = Metadata?.EfNodeSetting?.PluginSetting;
            if (setting == null || string.IsNullOrWhiteSpace(setting.PluginId) || string.IsNullOrWhiteSpace(setting.FunctionId))
            {
                CreateFailureExecLog(context.Workflow, dataContext, Metadata!, "插件节点未配置", startTime, DateTime.UtcNow.ToTimeStampMs(), true);
                return ExecutionResult.Next();
            }

            try
            {
                if (!IsPluginEnabled(dataContext.CorpId, setting.PluginId))
                {
                    CreateFailureExecLog(context.Workflow, dataContext, Metadata!, "插件未安装、已禁用或授权已过期", startTime, DateTime.UtcNow.ToTimeStampMs(), true);
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
                        new PluginExecArgs { FunName = setting.FunctionId, FunArgs = payload.SerializeToJson() },
                        invocationContext,
                        context.CancellationToken)
                    .GetAwaiter()
                    .GetResult();

                if (result.Code != 0)
                {
                    CreateFailureExecLog(context.Workflow, dataContext, Metadata!, result.Message ?? "插件执行失败", startTime, DateTime.UtcNow.ToTimeStampMs(), true);
                }
                else
                {
                    SavePluginNodeResult(dataContext, result.Result, setting);
                    CreateExecLog(context.Workflow, dataContext, Metadata!, startTime: startTime, endTime: DateTime.UtcNow.ToTimeStampMs(), summary: "执行成功");
                }
            }
            catch (Exception ex)
            {
                var failure = ClassifyFailure(Metadata!, ex, pluginFailure: true);
                dataContext.ErrMsg = failure.Reason;
                CreateExecLog(
                    context.Workflow,
                    dataContext,
                    Metadata!,
                    ex.Message,
                    startTime,
                    DateTime.UtcNow.ToTimeStampMs(),
                    failure.Reason,
                    failure.Suggestion,
                    failure.Summary);
                throw;
            }

            return ExecutionResult.Next();
        }

        private bool IsPluginEnabled(string corpId, string pluginId)
        {
            if (string.IsNullOrWhiteSpace(corpId) || string.IsNullOrWhiteSpace(pluginId))
            {
                return false;
            }

            var now = DateTime.UtcNow.ToTimeStampMs();
            return Resolver.Resolve<IRepository<PluginInstall>>().Queryable.Any(x =>
                x.CorpId == corpId
                && x.PluginId == pluginId
                && !x.DeleteFlag
                && x.Status == PluginInstallStatus.Installed
                && x.Enabled
                && (x.ExpireAt == null || x.ExpireAt > now));
        }

        private Dictionary<string, object?> BuildPayload(EfDataContext dataContext, Plugin.Contracts.PluginSetting setting)
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
            if (string.Equals(field.FieldType, PluginFieldKind.TableForm, StringComparison.OrdinalIgnoreCase)
                && field.SubFieldSettings.Count > 0)
            {
                return BuildSubListPayload(field, scriptData);
            }

            return field.ValueType switch
            {
                PluginValueType.Empty => null,
                PluginValueType.Field when field.ValueField != null => ResolveMappedFieldValue(field.ValueField, scriptData),
                _ => field.Value,
            };
        }

        private List<Dictionary<string, object?>> BuildSubListPayload(PluginFieldSetting field, Dictionary<string, object> scriptData)
        {
            var columns = field.SubFieldSettings
                .Select(subField => BuildSubListColumn(subField, scriptData))
                .ToList();
            var rowCount = columns
                .Where(x => x.RowValues != null)
                .Select(x => x.RowValues!.Count)
                .DefaultIfEmpty(columns.Any(x => x.CreatesRowWhenNoRowValues) ? 1 : 0)
                .Max();
            var rows = new List<Dictionary<string, object?>>();

            for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var column in columns)
                {
                    row[column.Setting.FieldKey] = column.RowValues != null
                        ? rowIndex < column.RowValues.Count ? column.RowValues[rowIndex] : null
                        : column.ScalarValue;
                }

                rows.Add(row);
            }

            return rows;
        }

        private SubListColumn BuildSubListColumn(PluginFieldSetting field, Dictionary<string, object> scriptData)
        {
            if (field.ValueType == PluginValueType.Field && field.ValueField != null)
            {
                var usesRowValues = UsesRowValues(field.ValueField);
                var value = ResolveMappedFieldValue(field.ValueField, scriptData, usesRowValues);
                return usesRowValues
                    ? new SubListColumn(field, ToValueList(value), null, false)
                    : new SubListColumn(field, null, value, true);
            }

            if (field.ValueType == PluginValueType.Empty)
            {
                return new SubListColumn(field, null, null, false);
            }

            return new SubListColumn(field, null, field.Value, true);
        }

        private static bool UsesRowValues(PluginFieldReference field)
        {
            return field.IsSubField || field.SingleResultNode == false;
        }

        private static List<object?>? ToValueList(object? value)
        {
            if (value == null)
            {
                return null;
            }

            if (value is JsonElement jsonElement)
            {
                value = ConvertJsonElement(jsonElement);
            }

            if (value is string text)
            {
                var trimmed = text.Trim();
                if (trimmed.StartsWith("[", StringComparison.Ordinal))
                {
                    try
                    {
                        var list = trimmed.DeserializeFromJson<List<object?>>();
                        if (list != null)
                        {
                            return NormalizeValueList(list);
                        }
                    }
                    catch (JsonException)
                    {
                    }
                }

                return [text];
            }

            if (value is IDictionary)
            {
                return [value];
            }

            if (value is IEnumerable enumerable and not string)
            {
                var list = new List<object?>();
                foreach (var item in enumerable)
                {
                    list.Add(NormalizeValueListItem(item));
                }

                return list;
            }

            return [value];
        }

        private static List<object?> NormalizeValueList(IEnumerable<object?> values)
        {
            var list = new List<object?>();
            foreach (var value in values)
            {
                list.Add(NormalizeValueListItem(value));
            }

            return list;
        }

        private static object? NormalizeValueListItem(object? value)
        {
            return value is JsonElement jsonElement ? ConvertJsonElement(jsonElement) : value;
        }

        private sealed record SubListColumn(
            PluginFieldSetting Setting,
            List<object?>? RowValues,
            object? ScalarValue,
            bool CreatesRowWhenNoRowValues);

        private object? ResolveMappedFieldValue(PluginFieldReference field, Dictionary<string, object> scriptData, bool asRowValues = false)
        {
            var value = ScriptEngine.Evaluate(BuildFieldExpression(field, asRowValues), scriptData).Value;
            if (!field.IsSubField)
            {
                return value;
            }

            if (value is not string && value is IEnumerable enumerable)
            {
                var list = new List<object?>();
                foreach (var item in enumerable)
                {
                    list.Add(item);
                }

                return list;
            }

            return value;
        }

        private static string BuildFieldExpression(PluginFieldReference field, bool asRowValues = false)
        {
            if (!field.IsSubField)
            {
                if (asRowValues && field.SingleResultNode == false)
                {
                    return $"MAP(data.n_{field.NodeId},'{field.Field}')";
                }

                return $"data.n_{field.NodeId}.{field.Field}";
            }

            var parts = field.Field.Split('>', 2, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 2
                ? $"MAP(data.n_{field.NodeId}.{parts[0]},'{parts[1]}')"
                : $"data.n_{field.NodeId}.{field.Field}";
        }

        private void SavePluginNodeResult(EfDataContext dataContext, object? pluginResult, Plugin.Contracts.PluginSetting setting)
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

            dataContext.NodeDatas[Metadata!.Id] = new EfNodeData
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

            var json = value.SerializeToJson();
            return json.DeserializeFromJson<Dictionary<string, object?>>()
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

            var type = value.GetType();
            if (!type.IsPrimitive
                && type != typeof(string)
                && type != typeof(decimal)
                && type != typeof(DateTime)
                && type != typeof(DateTimeOffset)
                && type != typeof(Guid)
                && !type.IsEnum)
            {
                return ToScriptValue(ToDictionary(value));
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
