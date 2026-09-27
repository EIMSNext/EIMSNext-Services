using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Async.RabbitMQ.Messaging;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.TestSupport;

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

        private sealed class TestEntity : IEntityKey
        {
            public string Id { get; set; } = string.Empty;
        }

        private sealed class ScopedRepository(ScopeMarker marker)
            : StubRepository<TestEntity>
        {
            public Guid ScopeId { get; } = marker.Id;
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
