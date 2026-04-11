using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Async.RabbitMQ.Messaging;
using EIMSNext.Core;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;

using HKH.Mef2.Integration;

using Microsoft.Extensions.DependencyInjection;

using RabbitMQ.Client;

using System.Composition.Hosting;

namespace EIMSNext.Async.Tests
{
    [TestClass]
    public class ConsumerScopeTests
    {
        [TestMethod]
        public async Task ExecuteInScopeAsync_CreatesNewScopePerExecution()
        {
            var observedScopeIds = new List<Guid>();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConnectionFactory, ConnectionFactory>();
            services.AddSingleton<IMessageRouteResolver, FakeMessageRouteResolver>();
            services.AddScoped<ScopeMarker>();
            services.AddScoped<IResolver, TestResolver>();

            await using var provider = services.BuildServiceProvider();
            var consumer = new TestConsumer(provider.GetRequiredService<IServiceScopeFactory>(), observedScopeIds);

            await consumer.ExecuteInScopeAsync(new TestMessage(), CancellationToken.None);
            await consumer.ExecuteInScopeAsync(new TestMessage(), CancellationToken.None);

            Assert.AreEqual(2, observedScopeIds.Count);
            Assert.AreNotEqual(observedScopeIds[0], observedScopeIds[1]);
        }

        [TestMethod]
        public async Task ExecuteInScopeAsync_ResolvesRepositoryThroughResolverWithinCurrentScope()
        {
            var observedRepositoryScopeIds = new List<Guid>();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConnectionFactory, ConnectionFactory>();
            services.AddSingleton<IMessageRouteResolver, FakeMessageRouteResolver>();
            services.AddScoped<ScopeMarker>();
            services.AddScoped<IRepository<TestEntity>, ScopedRepository>();
            services.AddScoped<IResolver, TestResolver>();

            await using var provider = services.BuildServiceProvider();
            var consumer = new RepositoryConsumer(provider.GetRequiredService<IServiceScopeFactory>(), observedRepositoryScopeIds);

            await consumer.ExecuteInScopeAsync(new TestMessage(), CancellationToken.None);
            await consumer.ExecuteInScopeAsync(new TestMessage(), CancellationToken.None);

            Assert.AreEqual(2, observedRepositoryScopeIds.Count);
            Assert.AreNotEqual(observedRepositoryScopeIds[0], observedRepositoryScopeIds[1]);
        }

        private sealed class TestConsumer(IServiceScopeFactory scopeFactory, List<Guid> observedScopeIds)
            : TaskConsumerBase<TestMessage, TestConsumer>(scopeFactory)
        {
            private readonly List<Guid> _observedScopeIds = observedScopeIds;

            protected override Task HandleAsync(TestMessage message, CancellationToken cancellationToken, IResolver resolver)
            {
                _observedScopeIds.Add(resolver.Resolve<ScopeMarker>().Id);
                return Task.CompletedTask;
            }
        }

        private sealed class RepositoryConsumer(IServiceScopeFactory scopeFactory, List<Guid> observedRepositoryScopeIds)
            : TaskConsumerBase<TestMessage, RepositoryConsumer>(scopeFactory)
        {
            private readonly List<Guid> _observedRepositoryScopeIds = observedRepositoryScopeIds;

            protected override Task HandleAsync(TestMessage message, CancellationToken cancellationToken, IResolver resolver)
            {
                var repository = (ScopedRepository)resolver.GetRepository<TestEntity>();
                _observedRepositoryScopeIds.Add(repository.ScopeId);
                return Task.CompletedTask;
            }
        }

        private sealed class TestMessage
        {
        }

        private sealed class ScopeMarker
        {
            public Guid Id { get; } = Guid.NewGuid();
        }

        private sealed class TestEntity : IMongoEntity
        {
            public string Id { get; set; } = string.Empty;
        }

        private sealed class ScopedRepository(ScopeMarker marker) : IRepository<TestEntity>
        {
            public Guid ScopeId { get; } = marker.Id;

            public EIMSNext.MongoDb.IMongoDbContex DbContext => throw new NotSupportedException();
            public MongoDB.Driver.IMongoCollection<TestEntity> Collection => throw new NotSupportedException();
            public IQueryable<TestEntity> Queryable => throw new NotSupportedException();
            public MongoDB.Driver.FilterDefinitionBuilder<TestEntity> FilterBuilder => throw new NotSupportedException();
            public MongoDB.Driver.SortDefinitionBuilder<TestEntity> SortBuilder => throw new NotSupportedException();
            public MongoDB.Driver.Search.SearchDefinitionBuilder<TestEntity> SearchBuilder => throw new NotSupportedException();
            public MongoDB.Driver.ProjectionDefinitionBuilder<TestEntity> ProjectionBuilder => throw new NotSupportedException();
            public MongoDB.Driver.UpdateDefinitionBuilder<TestEntity> UpdateBuilder => throw new NotSupportedException();
            public EIMSNext.Core.MongoDb.MongoTransactionScope NewTransactionScope(MongoDB.Driver.TransactionOptions? transOptions = null) => throw new NotSupportedException();
            public MongoDB.Driver.IFindFluent<TestEntity, TestEntity> Find(EIMSNext.Core.Query.DynamicFindOptions<TestEntity> options, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public MongoDB.Driver.IFindFluent<TestEntity, TestEntity> Find(EIMSNext.Core.Query.MongoFindOptions<TestEntity> options, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public MongoDB.Driver.IFindFluent<TestEntity, TestEntity> Find(System.Linq.Expressions.Expression<Func<TestEntity, bool>> filter, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<MongoDB.Driver.IAsyncCursor<TestEntity>> FindAsync(EIMSNext.Core.Query.DynamicFindOptions<TestEntity> options, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<MongoDB.Driver.IAsyncCursor<TestEntity>> FindAsync(EIMSNext.Core.Query.MongoFindOptions<TestEntity> options, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<MongoDB.Driver.IAsyncCursor<TestEntity>> FindAsync(System.Linq.Expressions.Expression<Func<TestEntity, bool>> filter, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public TestEntity? Get(string id, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<TestEntity?> GetAsync(string id, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public long Count(EIMSNext.Core.Query.DynamicFilter filter, MongoDB.Driver.IClientSessionHandle? session = null, MongoDB.Driver.CountOptions? options = null) => throw new NotSupportedException();
            public long Count(System.Linq.Expressions.Expression<Func<TestEntity, bool>> filter, MongoDB.Driver.IClientSessionHandle? session = null, MongoDB.Driver.CountOptions? options = null) => throw new NotSupportedException();
            public long Count(MongoDB.Driver.FilterDefinition<TestEntity> filter, MongoDB.Driver.IClientSessionHandle? session = null, MongoDB.Driver.CountOptions? options = null) => throw new NotSupportedException();
            public Task<long> CountAsync(EIMSNext.Core.Query.DynamicFilter filter, MongoDB.Driver.IClientSessionHandle? session = null, MongoDB.Driver.CountOptions? options = null) => throw new NotSupportedException();
            public Task<long> CountAsync(System.Linq.Expressions.Expression<Func<TestEntity, bool>> filter, MongoDB.Driver.IClientSessionHandle? session = null, MongoDB.Driver.CountOptions? options = null) => throw new NotSupportedException();
            public Task<long> CountAsync(MongoDB.Driver.FilterDefinition<TestEntity> filter, MongoDB.Driver.IClientSessionHandle? session = null, MongoDB.Driver.CountOptions? options = null) => throw new NotSupportedException();
            public void Insert(TestEntity entity, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public void Insert(IEnumerable<TestEntity> entities, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task InsertAsync(TestEntity entity, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task InsertAsync(IEnumerable<TestEntity> entities, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public MongoDB.Driver.UpdateResult Update(string id, MongoDB.Driver.UpdateDefinition<TestEntity> update, bool upsert = true, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<MongoDB.Driver.UpdateResult> UpdateAsync(string id, MongoDB.Driver.UpdateDefinition<TestEntity> update, bool upsert = true, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public MongoDB.Driver.UpdateResult UpdateMany(EIMSNext.Core.Query.DynamicFilter filter, MongoDB.Driver.UpdateDefinition<TestEntity> update, bool upsert = true, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<MongoDB.Driver.UpdateResult> UpdateManyAsync(EIMSNext.Core.Query.DynamicFilter filter, MongoDB.Driver.UpdateDefinition<TestEntity> update, bool upsert = true, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public MongoDB.Driver.UpdateResult UpdateMany(MongoDB.Driver.FilterDefinition<TestEntity> filter, MongoDB.Driver.UpdateDefinition<TestEntity> update, bool upsert = true, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<MongoDB.Driver.UpdateResult> UpdateManyAsync(MongoDB.Driver.FilterDefinition<TestEntity> filter, MongoDB.Driver.UpdateDefinition<TestEntity> update, bool upsert = true, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public MongoDB.Driver.ReplaceOneResult Replace(TestEntity entity, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<MongoDB.Driver.ReplaceOneResult> ReplaceAsync(TestEntity entity, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public MongoDB.Driver.DeleteResult Delete(string id, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public MongoDB.Driver.DeleteResult Delete(IEnumerable<string> ids, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public MongoDB.Driver.DeleteResult Delete(EIMSNext.Core.Query.DynamicFilter filter, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public MongoDB.Driver.DeleteResult Delete(MongoDB.Driver.FilterDefinition<TestEntity> filter, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<MongoDB.Driver.DeleteResult> DeleteAsync(string id, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<MongoDB.Driver.DeleteResult> DeleteAsync(IEnumerable<string> ids, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<MongoDB.Driver.DeleteResult> DeleteAsync(EIMSNext.Core.Query.DynamicFilter filter, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<MongoDB.Driver.DeleteResult> DeleteAsync(MongoDB.Driver.FilterDefinition<TestEntity> filter, MongoDB.Driver.IClientSessionHandle? session = null) => throw new NotSupportedException();
            public IEnumerable<TestEntity> EnsureId(IEnumerable<TestEntity> entities) => throw new NotSupportedException();
            public TestEntity EnsureId(TestEntity entity) => throw new NotSupportedException();
            public string NewId() => throw new NotSupportedException();
        }

        private sealed class FakeMessageRouteResolver : IMessageRouteResolver
        {
            public string ResolveQueueName(Type messageType) => "test-queue";
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
