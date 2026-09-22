using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Async.Tasks.Consumers;
using EIMSNext.Notification;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Repositories;
using EIMSNext.Entities;
using EIMSNext.TestSupport;

using HKH.Mef2.Integration;

using Microsoft.Extensions.DependencyInjection;

using RabbitMQ.Client;

using System.Composition.Hosting;
using System.Text.Json.Nodes;

namespace EIMSNext.Async.Tests
{
    [TestClass]
    public class WebhookConsumerTests
    {
        [TestMethod]
        public async Task ExecuteInScopeAsync_ShouldResolveWebhookConfigsAndInvokeEventHub()
        {
            var eventHub = new RecordingEventHub();
            var repository = new FakeWebhookRepository([
                new Webhook { Id = "wh-1", CorpId = "corp-1", AppId = "app-1", FormId = "form-1", Url = "https://example.com/1", SourceType = WebHookSource.Form, Triggers = (long)WebHookTrigger.Data_Updated, Disabled = false },
                new Webhook { Id = "wh-2", CorpId = "corp-1", AppId = "app-1", FormId = "form-1", Url = "https://example.com/2", SourceType = WebHookSource.Form, Triggers = (long)WebHookTrigger.Data_Updated, Disabled = false }
            ]);
            var aliasRepository = new FakeWebhookAliasRepository([
                new WebhookAlias
                {
                    Id = "wa-1",
                    CorpId = "corp-1",
                    AppId = "app-1",
                    FormId = "form-1",
                    FieldAlias = [
                        new FieldAliasItem { Field = "field1", Alias = "name" },
                        new FieldAliasItem
                        {
                            Field = "detail",
                            Alias = "items",
                            Children = [
                                new FieldAliasItem { Field = "col1", Alias = "price" },
                                new FieldAliasItem { Field = "col2", Alias = "qty" }
                            ]
                        }
                    ]
                }
            ]);

            var processingRepository = new FakeMessageProcessingRepository();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConnectionFactory, ConnectionFactory>();
            services.AddSingleton<IMessageRouteResolver, FakeMessageRouteResolver>();
            services.AddSingleton(eventHub);
            services.AddSingleton<IEventHub>(eventHub);
            services.AddSingleton<IMessageProcessingRepository>(processingRepository);
            services.AddSingleton<IOutboxIdempotencyKeyFactory, EIMSNext.Async.RabbitMQ.Outbox.OutboxIdempotencyKeyFactory>();
            services.AddSingleton<IRepository<Webhook>>(repository);
            services.AddSingleton<IRepository<WebhookAlias>>(aliasRepository);
            services.AddSingleton(new EIMSNext.Async.RabbitMQ.Messaging.ConsumerConcurrencyOptions());
            services.AddSingleton<Microsoft.Extensions.Options.IOptions<EIMSNext.Async.RabbitMQ.Messaging.ConsumerConcurrencyOptions>>(sp => Microsoft.Extensions.Options.Options.Create(sp.GetRequiredService<EIMSNext.Async.RabbitMQ.Messaging.ConsumerConcurrencyOptions>()));
            services.AddScoped<IResolver, TestResolver>();

            await using var provider = services.BuildServiceProvider();
            var consumer = new WebhookConsumer(provider.GetRequiredService<IServiceScopeFactory>());

            await consumer.ExecuteInScopeAsync(new WebhookTaskArgs
            {
                CorpId = "corp-1",
                AppId = "app-1",
                FormId = "form-1",
                Trigger = WebHookTrigger.Data_Updated,
                EventId = "event-1",
                PayloadJson = "{\"id\":\"data-1\",\"data\":{\"field1\":\"hello\",\"detail\":[{\"col1\":\"333\",\"col2\":444}]}}"
            }, CancellationToken.None);

            Assert.AreEqual(2, eventHub.Calls.Count);
            Assert.AreEqual("wh-1", eventHub.Calls[0].Webhook.Id);
            Assert.AreEqual("wh-2", eventHub.Calls[1].Webhook.Id);
            Assert.AreEqual(WebHookTrigger.Data_Updated, eventHub.Calls[0].Trigger);
            Assert.IsInstanceOfType<JsonNode>(eventHub.Calls[0].Data);
            var payload = (JsonNode)eventHub.Calls[0].Data;
            Assert.AreEqual("hello", payload?["data"]?["name"]?.GetValue<string>());
            Assert.IsNull(payload?["data"]?["field1"]);
            Assert.IsNotNull(payload?["data"]?["items"]);
            Assert.AreEqual("333", payload?["data"]?["items"]?[0]?["price"]?.GetValue<string>());
            Assert.AreEqual(444, payload?["data"]?["items"]?[0]?["qty"]?.GetValue<int>());
            CollectionAssert.AreEquivalent(new[] { "wh-1", "wh-2" }, processingRepository.CompletedTargets);
        }

        [TestMethod]
        public async Task ExecuteInScopeAsync_WhenWebhookFails_DoesNotRecordCompletionAndRequestsRequeue()
        {
            var processingRepository = new FakeMessageProcessingRepository();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConnectionFactory, ConnectionFactory>();
            services.AddSingleton<IMessageRouteResolver, FakeMessageRouteResolver>();
            services.AddSingleton<IEventHub, ThrowingEventHub>();
            services.AddSingleton<IMessageProcessingRepository>(processingRepository);
            services.AddSingleton<IOutboxIdempotencyKeyFactory, EIMSNext.Async.RabbitMQ.Outbox.OutboxIdempotencyKeyFactory>();
            services.AddSingleton<IRepository<Webhook>>(new FakeWebhookRepository([
                new Webhook { Id = "wh-1", CorpId = "corp-1", AppId = "app-1", FormId = "form-1", Url = "https://example.com/1", SourceType = WebHookSource.Form, Triggers = (long)WebHookTrigger.Data_Updated }
            ]));
            services.AddSingleton<IRepository<WebhookAlias>>(new FakeWebhookAliasRepository([]));
            services.AddSingleton(new EIMSNext.Async.RabbitMQ.Messaging.ConsumerConcurrencyOptions());
            services.AddSingleton<Microsoft.Extensions.Options.IOptions<EIMSNext.Async.RabbitMQ.Messaging.ConsumerConcurrencyOptions>>(sp => Microsoft.Extensions.Options.Options.Create(sp.GetRequiredService<EIMSNext.Async.RabbitMQ.Messaging.ConsumerConcurrencyOptions>()));
            services.AddScoped<IResolver, TestResolver>();

            await using var provider = services.BuildServiceProvider();
            var consumer = new WebhookConsumer(provider.GetRequiredService<IServiceScopeFactory>());
            var message = new WebhookTaskArgs
            {
                CorpId = "corp-1",
                AppId = "app-1",
                FormId = "form-1",
                Trigger = WebHookTrigger.Data_Updated,
                EventId = "event-1",
                PayloadJson = "{\"id\":\"data-1\"}"
            };

            await Assert.ThrowsExactlyAsync<EIMSNext.Async.RabbitMQ.Messaging.TaskRequeueException>(
                () => consumer.ExecuteInScopeAsync(message, CancellationToken.None));
            Assert.AreEqual(0, processingRepository.CompletedTargets.Count);
        }

        private sealed class RecordingEventHub : IEventHub
        {
            public List<(Webhook Webhook, WebHookTrigger Trigger, object Data)> Calls { get; } = [];

            public Task SendAsync(Webhook webhook, WebHookTrigger trigger, string eventId, object data)
            {
                Calls.Add((webhook, trigger, data));
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// 内存版 Webhook 仓储。
        /// <para>
        /// 迁移说明：原实现逐个实现了 Mongo 时代的 <c>IRepository&lt;T&gt;</c> 成员
        /// （<c>IMongoCollection</c> / <c>FilterDefinitionBuilder</c> / <c>IClientSessionHandle</c> /
        /// <c>IFindFluent</c> / <c>BsonValue</c> 等）。PostgreSQL/EF Core 迁移后仓储接口只基于
        /// EF Core，除 <see cref="Queryable"/> 之外的能力对本测试都无意义，因此改为继承
        /// <see cref="StubRepository{T}"/> 并只覆写真正被用到的 <see cref="Queryable"/>。
        /// </para>
        /// </summary>
        private sealed class FakeWebhookRepository(List<Webhook> webhooks)
            : StubRepository<Webhook>
        {
            public override IQueryable<Webhook> Queryable => webhooks.AsQueryable();
        }

        private sealed class FakeWebhookAliasRepository(List<WebhookAlias> aliases)
            : StubRepository<WebhookAlias>
        {
            public override IQueryable<WebhookAlias> Queryable => aliases.AsQueryable();
        }

        private sealed class FakeMessageProcessingRepository : IMessageProcessingRepository
        {
            public List<string> CompletedTargets { get; } = [];

            public Task<string?> TryAcquireAsync(string eventKey, string target, DateTime leaseUntil, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<string?>("lease-token");
            }

            public Task<bool> MarkCompletedAsync(string eventKey, string target, string leaseToken, long processedTime, CancellationToken cancellationToken = default)
            {
                CompletedTargets.Add(target);
                return Task.FromResult(true);
            }
        }

        private sealed class ThrowingEventHub : IEventHub
        {
            public Task SendAsync(Webhook webhook, WebHookTrigger trigger, string eventId, object data)
                => throw new HttpRequestException("Webhook unavailable.");
        }

        private sealed class FakeMessageRouteResolver : IMessageRouteResolver
        {
            public string ResolveQueueName(Type messageType) => "webhook";
        }

        private sealed class TestResolver(IServiceProvider serviceProvider) : IResolver
        {
            public CompositionContainer MefContainer => throw new NotSupportedException();

            public object Resolve(Type type, string? name = null) => serviceProvider.GetRequiredService(type);

            public T Resolve<T>(string? name = null) where T : class => serviceProvider.GetRequiredService<T>();

            public T GetExport<T>(string? name = null) where T : class => serviceProvider.GetRequiredService<T>();

            public object GetExport(Type type, string? name = null) => serviceProvider.GetRequiredService(type);

            public IEnumerable<T> GetExports<T>(string? name = null) where T : class => serviceProvider.GetServices<T>();

            public IEnumerable<object> GetExports(Type type, string? name = null) => serviceProvider.GetServices(type).Cast<object>();
        }
    }
}
