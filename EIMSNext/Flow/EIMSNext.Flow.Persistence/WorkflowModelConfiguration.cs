using System.Dynamic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Persistence;

public static class WorkflowModelConfiguration
{
    public static void ConfigureWorkflowStore(this ModelBuilder builder)
    {
        var workflow = builder.Entity<WorkflowInstance>();
        workflow.ToTable(nameof(WorkflowInstance));
        workflow.HasKey(x => x.Id);
        workflow.Property(x => x.Id).ValueGeneratedNever();
        Json(workflow.Property(x => x.Data));

        // 指针由 PostgreSqlPersistenceProvider 逐指针差量同步，这里必须把导航属性从映射中拿掉，避免 EF 按关系约定自动生成外键。
        workflow.Ignore(x => x.ExecutionPointers);
        workflow.HasIndex(x => new { x.Status, x.NextExecution });
        workflow.HasIndex(x => new { x.Reference, x.Status, x.CreateTime });
        workflow.HasIndex(x => new { x.WorkflowDefinitionId, x.Status });

        // WorkflowCore 运行时模型没有 WorkflowId（它在引擎里只作为 WorkflowInstance 的成员存在），
        // 用影子属性补上，作为与 WorkflowInstance 的关联键。
        var pointer = builder.Entity<ExecutionPointer>();
        pointer.ToTable(nameof(ExecutionPointer));
        pointer.HasKey(x => x.Id);
        pointer.Property(x => x.Id).ValueGeneratedNever();
        pointer.Property<string>("WorkflowId").IsRequired();
        // 把外键关系补进模型：EF 才能保证先插实例、后插指针（否则同一批 SaveChanges
        // 里指针行可能先落库，被 DB 层外键拒绝），并让删除语义与 DB 级联一致。
        pointer.HasOne<WorkflowInstance>()
            .WithMany()
            .HasForeignKey("WorkflowId")
            .OnDelete(DeleteBehavior.Cascade);
        pointer.HasIndex("WorkflowId");
        Json(pointer.Property(x => x.PersistenceData));
        Json(pointer.Property(x => x.EventData));
        Json(pointer.Property(x => x.ContextItem));
        Json(pointer.Property(x => x.Outcome));
        Json(pointer.Property(x => x.ExtensionAttributes));
        Json(pointer.Property(x => x.Children));
        Json(pointer.Property(x => x.Scope));

        var subscription = builder.Entity<EventSubscription>();
        subscription.ToTable(nameof(EventSubscription));
        subscription.HasKey(x => x.Id);
        subscription.Property(x => x.Id).ValueGeneratedNever();
        Json(subscription.Property(x => x.SubscriptionData));
        subscription.HasIndex(x => new { x.EventName, x.EventKey, x.SubscribeAsOf, x.ExternalToken });
        subscription.HasIndex(x => x.WorkflowId);

        var events = builder.Entity<Event>();
        events.ToTable(nameof(Event));
        events.HasKey(x => x.Id);
        events.Property(x => x.Id).ValueGeneratedNever();
        Json(events.Property(x => x.EventData));
        events.HasIndex(x => new { x.IsProcessed, x.EventTime });
        events.HasIndex(x => new { x.EventName, x.EventKey, x.EventTime });

        var error = builder.Entity<ExecutionError>();
        error.ToTable(nameof(ExecutionError));
        error.Property<long>("Id").UseIdentityByDefaultColumn();
        error.HasKey("Id");
        error.HasIndex(x => x.WorkflowId);

        var command = builder.Entity<ScheduledCommand>();
        command.ToTable(nameof(ScheduledCommand));
        command.Property<long>("Id").UseIdentityByDefaultColumn();
        command.HasKey("Id");
        command.Property(x => x.CommandName).IsRequired();
        command.Property(x => x.Data).IsRequired();
        command.HasIndex(x => new { x.CommandName, x.Data }).IsUnique();
        command.HasIndex(x => x.ExecuteTime);

        foreach (var entity in builder.Model.GetEntityTypes())
        foreach (var property in entity.GetProperties())
        {
            if (property.ClrType == typeof(DateTime))
                property.SetValueConverter(new ValueConverter<DateTime, DateTime>(
                    value => value.ToUniversalTime(), value => DateTime.SpecifyKind(value, DateTimeKind.Utc)));
            else if (property.ClrType == typeof(DateTime?))
                property.SetValueConverter(new ValueConverter<DateTime?, DateTime?>(
                    value => value.HasValue ? value.Value.ToUniversalTime() : null,
                    value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null));
        }
    }

    private static void Json<T>(PropertyBuilder<T> property)
    {
        property.HasConversion(value => RuntimeJson.Write(value), json => RuntimeJson.Read<T>(json))
            .HasColumnType("jsonb");
        property.Metadata.SetValueComparer(new ValueComparer<T>(
            (left, right) => RuntimeJson.Write(left) == RuntimeJson.Write(right),
            value => RuntimeJson.Write(value).GetHashCode(),
            value => RuntimeJson.Read<T>(RuntimeJson.Write(value))));
    }

    private static class RuntimeJson
    {
        private static readonly JsonSerializerSettings Settings = new()
        {
            // WorkflowCore 的 WorkflowInstance.Data / ExecutionPointer.PersistenceData / EventSubscription.SubscriptionData
            // 声明类型都是 object。若不带类型名，写入 ControlPersistenceData 这类具体状态后读回来只剩
            // ExpandoObject，工作流恢复执行时状态类型不符，节点会拿到错误的载荷。
            //   Objects = 所有复杂对象都写 $type（含 object 声明属性上的具体状态类型）；
            //   数组不写（Arrays 才写），所以 object[] 仍旧是纯 JSON 数组，读回来还是 object[]。
            // 注意 Auto 在这里不适用：它对「object 声明 + 具体运行时类型」的组合不落 $type，
            // ControlPersistenceData 会被静默降级成 ExpandoObject。
            //
            // 关于 TypeNameHandling 的安全取舍：这里的输入只来自本应用自己的持久化写入
            // （jsonb 列，不是外部请求体），没有把不可信 JSON 直接喂进来的路径；
            // 反过来，若加一份类型白名单，漏列某个集合/状态类型会让工作流恢复直接失败，
            // 运维风险高于理论攻击面。因此这里用 Objects 而不是白名单 Binder。
            TypeNameHandling = TypeNameHandling.Objects,
            DateParseHandling = DateParseHandling.None,
            // 运行期对象图可能成环，成环会让整笔 PersistWorkflow 事务失败、实例被反复重跑，
            // 忽略重复引用即可让 WorkflowInstance 状态正常落库。
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            Converters = { new RuntimeObjectConverter(), new StepExecutionContextConverter() }
        };

        public static string Write<T>(T value) => JsonConvert.SerializeObject(value, Settings);
        public static T Read<T>(string json) => JsonConvert.DeserializeObject<T>(json, Settings)!;

        // Workflow payloads use ExpandoObject, not JObject/JsonElement. Keep that
        // contract when reading object-valued data and execution pointer fields.
        private sealed class RuntimeObjectConverter : JsonConverter
        {
            public override bool CanWrite => false;
            public override bool CanConvert(Type objectType) => objectType == typeof(object);

            public override object? ReadJson(JsonReader reader, Type type, object? existingValue, JsonSerializer serializer)
            {
                var token = JToken.Load(reader);

                // 目标类型是 object 时 Newtonsoft 会优先选中本转换器，于是它自己的 $type 解析
                // （ResolveTypeName）被整个绕过 —— 类型名只能在这里还原，否则写入的
                // ControlPersistenceData 读回来又变成 ExpandoObject。
                if (token is JObject typed
                    && typed.TryGetValue("$type", out var typeToken)
                    && typeToken.Type == JTokenType.String
                    && typeToken.Value<string>() is { } typeName
                    && ResolveStateType(typeName) is { } stateType)
                {
                    var state = (JObject)typed.DeepClone();
                    state.Remove("$type");
                    return state.ToObject(stateType, serializer);
                }

                return Convert(token);
            }

            /// <summary>
            /// 解析 <c>$type</c>：ExpandoObject 仍走字典分支（返回 null 表示「按无类型处理」），
            /// 其余类型交回 Newtonsoft 按具体类型构造。
            /// </summary>
            private static Type? ResolveStateType(string typeName)
            {
                var resolved = Type.GetType(typeName, throwOnError: false);
                return resolved is null || resolved == typeof(ExpandoObject) ? null : resolved;
            }

            public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
                => throw new NotSupportedException("Runtime objects use the default JSON writer.");

            private static object? Convert(JToken token)
            {
                if (token is JObject obj)
                {
                    // 若某个数组被写成了 typed 形式 { "$type": "...[]", "$values": [ ... ] }
                    // （TypeNameHandling 含 Arrays 时会出现），这里必须还原成真正的数组，
                    // 否则整块会退化成带 $type/$values 字段的 ExpandoObject，
                    // 工作流恢复时拿到的就不是数组（历史缺陷：Items 变 ExpandoObject）。
                    if (obj.TryGetValue("$type", out var typeToken)
                        && obj.TryGetValue("$values", out var values)
                        && typeToken.Type == JTokenType.String
                        && values is JArray typedArray)
                        return typedArray.Select(Convert).ToArray();

                    IDictionary<string, object?> result = new ExpandoObject();
                    foreach (var property in obj.Properties())
                    {
                        // $type 只是类型标记，不进业务字典（ExpandoObject 是调用方直接遍历的载荷）。
                        if (property.Name == "$type") continue;
                        result[property.Name] = Convert(property.Value);
                    }
                    return result;
                }
                if (token is JArray array) return array.Select(Convert).ToArray();
                return (token as JValue)?.Value;
            }
        }

        // ExecutionResult.Persist(context) 会把整个运行期上下文写进 ExecutionPointer.PersistenceData，
        // 而 StepExecutionContext 反向引用 WorkflowInstance、并持有带 LambdaExpression 的 Step
        // （输出映射 MemberMapParameter），在 jsonb 里既会成环、又无法反序列化 —— 导致整笔
        // PersistWorkflow 失败、实例状态不落地并被引擎反复重跑。运行期上下文由引擎重新构建，
        // 落库时只保留其中真正的持久化载荷。
        private sealed class StepExecutionContextConverter : JsonConverter
        {
            public override bool CanRead => false;

            public override bool CanConvert(Type objectType) => typeof(IStepExecutionContext).IsAssignableFrom(objectType);

            public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
                => serializer.Serialize(writer, ((IStepExecutionContext)value!).PersistenceData);

            public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
                => throw new NotSupportedException();
        }
    }
}
