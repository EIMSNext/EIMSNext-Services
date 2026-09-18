using System.Dynamic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        Json(workflow.Property(x => x.ExecutionPointers));
        workflow.HasIndex(x => new { x.Status, x.NextExecution });
        workflow.HasIndex(x => new { x.Reference, x.Status, x.CreateTime });
        workflow.HasIndex(x => new { x.WorkflowDefinitionId, x.Status });

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
            TypeNameHandling = TypeNameHandling.None,
            DateParseHandling = DateParseHandling.None,
            Converters = { new RuntimeObjectConverter() }
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
                => Convert(JToken.Load(reader));
            public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
                => throw new NotSupportedException("Runtime objects use the default JSON writer.");

            private static object? Convert(JToken token)
            {
                if (token is JObject obj)
                {
                    IDictionary<string, object?> result = new ExpandoObject();
                    foreach (var property in obj.Properties()) result[property.Name] = Convert(property.Value);
                    return result;
                }
                if (token is JArray array) return array.Select(Convert).ToArray();
                return (token as JValue)?.Value;
            }
        }
    }
}
