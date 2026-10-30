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
        public async Task ExecuteInScopeAsync_ShouldPassChainJsonToEventHub_ForLoopBackChain()
        {
            // 回归锁：链上表单写入 → WebhookTaskArgs.ChainJson → EventHub（X-EF-Chain header 透传给入站端点恢复链）
            var eventHub = new RecordingEventHub();
            var repository = new FakeWebhookRepository([
                new Webhook { Id = "wh-1", CorpId = "corp-1", AppId = "app-1", FormId = "form-1", Url = "https://example.com/1", SourceType = WebHookSource.Form, Triggers = (long)WebHookTrigger.Data_Created, Disabled = false }
            ]);

            var processingRepository = new FakeMessageProcessingRepository();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConnectionFactory, ConnectionFactory>();
            services.AddSingleton<IMessageRouteResolver, FakeMessageRouteResolver>();
            services.AddSingleton<IEventHub>(eventHub);
            services.AddSingleton<IMessageProcessingRepository>(processingRepository);
            services.AddSingleton<IOutboxIdempotencyKeyFactory, EIMSNext.Async.RabbitMQ.Outbox.OutboxIdempotencyKeyFactory>();
            services.AddSingleton<IRepository<Webhook>>(repository);
            services.AddSingleton<IRepository<WebhookAlias>>(new FakeWebhookAliasRepository([]));
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
                Trigger = WebHookTrigger.Data_Created,
                EventId = "event-chain",
                PayloadJson = "{\"id\":\"data-1\"}",
                ChainJson = "{\"ChainId\":\"chain-1\",\"Depth\":2,\"Path\":[\"ef-a\",\"ef-b\"]}"
            }, CancellationToken.None);

            Assert.AreEqual(1, eventHub.Calls.Count);
            Assert.AreEqual("{\"ChainId\":\"chain-1\",\"Depth\":2,\"Path\":[\"ef-a\",\"ef-b\"]}", eventHub.Calls[0].ChainJson);
        }

        [TestMethod]
        public async Task ExecuteInScopeAsync_ShouldPassNullChainJson_WhenMessageHasNoChain()
        {
            // 链外消息（用户直接操作/老消息无该字段）：ChainJson 为 null，入站端点视作新链
            var eventHub = new RecordingEventHub();
            var repository = new FakeWebhookRepository([
                new Webhook { Id = "wh-1", CorpId = "corp-1", AppId = "app-1", FormId = "form-1", Url = "https://example.com/1", SourceType = WebHookSource.Form, Triggers = (long)WebHookTrigger.Data_Created, Disabled = false }
            ]);

            var processingRepository = new FakeMessageProcessingRepository();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConnectionFactory, ConnectionFactory>();
            services.AddSingleton<IMessageRouteResolver, FakeMessageRouteResolver>();
            services.AddSingleton<IEventHub>(eventHub);
            services.AddSingleton<IMessageProcessingRepository>(processingRepository);
            services.AddSingleton<IOutboxIdempotencyKeyFactory, EIMSNext.Async.RabbitMQ.Outbox.OutboxIdempotencyKeyFactory>();
            services.AddSingleton<IRepository<Webhook>>(repository);
            services.AddSingleton<IRepository<WebhookAlias>>(new FakeWebhookAliasRepository([]));
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
                Trigger = WebHookTrigger.Data_Created,
                EventId = "event-nochain",
                PayloadJson = "{\"id\":\"data-1\"}"
            }, CancellationToken.None);

            Assert.AreEqual(1, eventHub.Calls.Count);
            Assert.IsNull(eventHub.Calls[0].ChainJson);
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
            CollectionAssert.AreEqual(new[] { "wh-1" }, processingRepository.ReleasedTargets);
        }

        private sealed class RecordingEventHub : IEventHub
        {
            public List<(Webhook Webhook, WebHookTrigger Trigger, object Data, string? ChainJson)> Calls { get; } = [];

            public Task SendAsync(Webhook webhook, WebHookTrigger trigger, string eventId, object data, string? chainJson = null)
            {
                Calls.Add((webhook, trigger, data, chainJson));
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// 内存版 Webhook 仓储。
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
            public List<string> ReleasedTargets { get; } = [];

            public Task<string?> TryAcquireAsync(string eventKey, string target, DateTime leaseUntil, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<string?>("lease-token");
            }

            public Task<bool> IsCompletedAsync(string eventKey, string target, CancellationToken cancellationToken = default)
                => Task.FromResult(false);

            public Task<bool> MarkCompletedAsync(string eventKey, string target, string leaseToken, long processedTime, CancellationToken cancellationToken = default)
            {
                CompletedTargets.Add(target);
                return Task.FromResult(true);
            }

            public Task<bool> ReleaseAsync(string eventKey, string target, string leaseToken, CancellationToken cancellationToken = default)
            {
                ReleasedTargets.Add(target);
                return Task.FromResult(true);
            }
        }

        private sealed class ThrowingEventHub : IEventHub
        {
            public Task SendAsync(Webhook webhook, WebHookTrigger trigger, string eventId, object data, string? chainJson = null)
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
