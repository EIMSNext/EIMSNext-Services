using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Async.Tasks.Consumers;
using EIMSNext.CloudEvent;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Query;
using EIMSNext.Core.Repositories;
using EIMSNext.Service.Entities;

using HKH.Mef2.Integration;

using Microsoft.Extensions.DependencyInjection;

using MongoDB.Driver;

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

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConnectionFactory, ConnectionFactory>();
            services.AddSingleton<IMessageRouteResolver, FakeMessageRouteResolver>();
            services.AddSingleton(eventHub);
            services.AddSingleton<IEventHub>(eventHub);
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
        }

        private sealed class RecordingEventHub : IEventHub
        {
            public List<(Webhook Webhook, WebHookTrigger Trigger, object Data)> Calls { get; } = [];

            public Task SendAsync(Webhook webhook, WebHookTrigger trigger, object data)
            {
                Calls.Add((webhook, trigger, data));
                return Task.CompletedTask;
            }
        }

        private sealed class FakeWebhookRepository(List<Webhook> webhooks) : IRepository<Webhook>
        {
            public EIMSNext.MongoDb.IMongoDbContex DbContext => throw new NotSupportedException();
            public MongoDB.Driver.IMongoCollection<Webhook> Collection => throw new NotSupportedException();
            public IQueryable<Webhook> Queryable => webhooks.AsQueryable();
            public MongoDB.Driver.FilterDefinitionBuilder<Webhook> FilterBuilder => Builders<Webhook>.Filter;
            public MongoDB.Driver.SortDefinitionBuilder<Webhook> SortBuilder => Builders<Webhook>.Sort;
            public MongoDB.Driver.Search.SearchDefinitionBuilder<Webhook> SearchBuilder => Builders<Webhook>.Search;
            public MongoDB.Driver.ProjectionDefinitionBuilder<Webhook> ProjectionBuilder => Builders<Webhook>.Projection;
            public MongoDB.Driver.UpdateDefinitionBuilder<Webhook> UpdateBuilder => Builders<Webhook>.Update;
            public EIMSNext.Core.MongoDb.MongoTransactionScope NewTransactionScope(MongoDB.Driver.TransactionOptions? transOptions = null) => throw new NotSupportedException();
            public MongoDB.Driver.IFindFluent<Webhook, Webhook> Find(EIMSNext.Core.Query.DynamicFindOptions<Webhook> options, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public MongoDB.Driver.IFindFluent<Webhook, Webhook> Find(EIMSNext.Core.Query.MongoFindOptions<Webhook> options, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public MongoDB.Driver.IFindFluent<Webhook, Webhook> Find(System.Linq.Expressions.Expression<Func<Webhook, bool>> filter, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<MongoDB.Driver.IAsyncCursor<Webhook>> FindAsync(EIMSNext.Core.Query.DynamicFindOptions<Webhook> options, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<MongoDB.Driver.IAsyncCursor<Webhook>> FindAsync(EIMSNext.Core.Query.MongoFindOptions<Webhook> options, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<MongoDB.Driver.IAsyncCursor<Webhook>> FindAsync(System.Linq.Expressions.Expression<Func<Webhook, bool>> filter, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Webhook? Get(string id, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<Webhook?> GetAsync(string id, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public long Count(DynamicFilter filter, MongoDB.Driver.IClientSessionHandle? session = null, MongoDB.Driver.CountOptions? options = null) => throw new NotSupportedException();
            public long Count(System.Linq.Expressions.Expression<Func<Webhook, bool>> filter, MongoDB.Driver.IClientSessionHandle? session = null, MongoDB.Driver.CountOptions? options = null) => throw new NotSupportedException();
            public long Count(MongoDB.Driver.FilterDefinition<Webhook> filter, MongoDB.Driver.IClientSessionHandle? session = null, MongoDB.Driver.CountOptions? options = null) => throw new NotSupportedException();
            public Task<long> CountAsync(DynamicFilter filter, MongoDB.Driver.IClientSessionHandle? session = null, MongoDB.Driver.CountOptions? options = null) => throw new NotSupportedException();
            public Task<long> CountAsync(System.Linq.Expressions.Expression<Func<Webhook, bool>> filter, MongoDB.Driver.IClientSessionHandle? session = null, MongoDB.Driver.CountOptions? options = null) => throw new NotSupportedException();
            public Task<long> CountAsync(MongoDB.Driver.FilterDefinition<Webhook> filter, MongoDB.Driver.IClientSessionHandle? session = null, MongoDB.Driver.CountOptions? options = null) => throw new NotSupportedException();
            public void Insert(Webhook entity, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public void Insert(IEnumerable<Webhook> entities, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task InsertAsync(Webhook entity, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task InsertAsync(IEnumerable<Webhook> entities, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public MongoDB.Driver.UpdateResult Update(string id, MongoDB.Driver.UpdateDefinition<Webhook> update, bool upsert = true, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<MongoDB.Driver.UpdateResult> UpdateAsync(string id, MongoDB.Driver.UpdateDefinition<Webhook> update, bool upsert = true, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public MongoDB.Driver.UpdateResult UpdateMany(DynamicFilter filter, MongoDB.Driver.UpdateDefinition<Webhook> update, bool upsert = true, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<MongoDB.Driver.UpdateResult> UpdateManyAsync(DynamicFilter filter, MongoDB.Driver.UpdateDefinition<Webhook> update, bool upsert = true, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public MongoDB.Driver.UpdateResult UpdateMany(MongoDB.Driver.FilterDefinition<Webhook> filter, MongoDB.Driver.UpdateDefinition<Webhook> update, bool upsert = true, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<MongoDB.Driver.UpdateResult> UpdateManyAsync(MongoDB.Driver.FilterDefinition<Webhook> filter, MongoDB.Driver.UpdateDefinition<Webhook> update, bool upsert = true, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public MongoDB.Driver.ReplaceOneResult Replace(Webhook entity, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<MongoDB.Driver.ReplaceOneResult> ReplaceAsync(Webhook entity, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public MongoDB.Driver.DeleteResult Delete(string id, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public MongoDB.Driver.DeleteResult Delete(IEnumerable<string> ids, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public MongoDB.Driver.DeleteResult Delete(DynamicFilter filter, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public MongoDB.Driver.DeleteResult Delete(MongoDB.Driver.FilterDefinition<Webhook> filter, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<MongoDB.Driver.DeleteResult> DeleteAsync(string id, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<MongoDB.Driver.DeleteResult> DeleteAsync(IEnumerable<string> ids, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<MongoDB.Driver.DeleteResult> DeleteAsync(DynamicFilter filter, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<MongoDB.Driver.DeleteResult> DeleteAsync(MongoDB.Driver.FilterDefinition<Webhook> filter, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public IEnumerable<Webhook> EnsureId(IEnumerable<Webhook> entities) => throw new NotSupportedException();
            public Webhook EnsureId(Webhook entity) => throw new NotSupportedException();
            public string NewId() => throw new NotSupportedException();
        }

        private sealed class FakeWebhookAliasRepository(List<WebhookAlias> aliases) : IRepository<WebhookAlias>
        {
            public EIMSNext.MongoDb.IMongoDbContex DbContext => throw new NotSupportedException();
            public IMongoCollection<WebhookAlias> Collection => throw new NotSupportedException();
            public IQueryable<WebhookAlias> Queryable => aliases.AsQueryable();
            public FilterDefinitionBuilder<WebhookAlias> FilterBuilder => Builders<WebhookAlias>.Filter;
            public SortDefinitionBuilder<WebhookAlias> SortBuilder => Builders<WebhookAlias>.Sort;
            public MongoDB.Driver.Search.SearchDefinitionBuilder<WebhookAlias> SearchBuilder => Builders<WebhookAlias>.Search;
            public ProjectionDefinitionBuilder<WebhookAlias> ProjectionBuilder => Builders<WebhookAlias>.Projection;
            public UpdateDefinitionBuilder<WebhookAlias> UpdateBuilder => Builders<WebhookAlias>.Update;
            public EIMSNext.Core.MongoDb.MongoTransactionScope NewTransactionScope(TransactionOptions? transOptions = null) => throw new NotSupportedException();
            public IFindFluent<WebhookAlias, WebhookAlias> Find(EIMSNext.Core.Query.DynamicFindOptions<WebhookAlias> options, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public IFindFluent<WebhookAlias, WebhookAlias> Find(EIMSNext.Core.Query.MongoFindOptions<WebhookAlias> options, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public IFindFluent<WebhookAlias, WebhookAlias> Find(System.Linq.Expressions.Expression<Func<WebhookAlias, bool>> filter, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<IAsyncCursor<WebhookAlias>> FindAsync(EIMSNext.Core.Query.DynamicFindOptions<WebhookAlias> options, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<IAsyncCursor<WebhookAlias>> FindAsync(EIMSNext.Core.Query.MongoFindOptions<WebhookAlias> options, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<IAsyncCursor<WebhookAlias>> FindAsync(System.Linq.Expressions.Expression<Func<WebhookAlias, bool>> filter, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public WebhookAlias? Get(string id, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<WebhookAlias?> GetAsync(string id, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public long Count(DynamicFilter filter, IClientSessionHandle? session = null, CountOptions? options = null) => throw new NotSupportedException();
            public long Count(System.Linq.Expressions.Expression<Func<WebhookAlias, bool>> filter, IClientSessionHandle? session = null, CountOptions? options = null) => throw new NotSupportedException();
            public long Count(FilterDefinition<WebhookAlias> filter, IClientSessionHandle? session = null, CountOptions? options = null) => throw new NotSupportedException();
            public Task<long> CountAsync(DynamicFilter filter, IClientSessionHandle? session = null, CountOptions? options = null) => throw new NotSupportedException();
            public Task<long> CountAsync(System.Linq.Expressions.Expression<Func<WebhookAlias, bool>> filter, IClientSessionHandle? session = null, CountOptions? options = null) => throw new NotSupportedException();
            public Task<long> CountAsync(FilterDefinition<WebhookAlias> filter, IClientSessionHandle? session = null, CountOptions? options = null) => throw new NotSupportedException();
            public void Insert(WebhookAlias entity, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public void Insert(IEnumerable<WebhookAlias> entities, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task InsertAsync(WebhookAlias entity, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task InsertAsync(IEnumerable<WebhookAlias> entities, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public UpdateResult Update(string id, UpdateDefinition<WebhookAlias> update, bool upsert = true, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<UpdateResult> UpdateAsync(string id, UpdateDefinition<WebhookAlias> update, bool upsert = true, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public UpdateResult UpdateMany(DynamicFilter filter, UpdateDefinition<WebhookAlias> update, bool upsert = true, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<UpdateResult> UpdateManyAsync(DynamicFilter filter, UpdateDefinition<WebhookAlias> update, bool upsert = true, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public UpdateResult UpdateMany(FilterDefinition<WebhookAlias> filter, UpdateDefinition<WebhookAlias> update, bool upsert = true, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<UpdateResult> UpdateManyAsync(FilterDefinition<WebhookAlias> filter, UpdateDefinition<WebhookAlias> update, bool upsert = true, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public ReplaceOneResult Replace(WebhookAlias entity, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<ReplaceOneResult> ReplaceAsync(WebhookAlias entity, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public DeleteResult Delete(string id, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public DeleteResult Delete(IEnumerable<string> ids, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public DeleteResult Delete(DynamicFilter filter, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public DeleteResult Delete(FilterDefinition<WebhookAlias> filter, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<DeleteResult> DeleteAsync(string id, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<DeleteResult> DeleteAsync(IEnumerable<string> ids, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<DeleteResult> DeleteAsync(DynamicFilter filter, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<DeleteResult> DeleteAsync(FilterDefinition<WebhookAlias> filter, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public IEnumerable<WebhookAlias> EnsureId(IEnumerable<WebhookAlias> entities) => throw new NotSupportedException();
            public WebhookAlias EnsureId(WebhookAlias entity) => throw new NotSupportedException();
            public string NewId() => throw new NotSupportedException();
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
