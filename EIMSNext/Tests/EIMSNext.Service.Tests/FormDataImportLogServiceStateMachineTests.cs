using System.Composition.Hosting;
using System.Linq.Expressions;
using System.Reflection;

using EIMSNext.Cache;
using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Service.Contracts;
using EIMSNext.Entities;
using EIMSNext.TestSupport;

using HKH.Mef2.Integration;

using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EIMSNext.Service.Tests
{
    /// <summary>
    /// <see cref="FormDataImportLogService"/> 状态机测试。
    /// <para>
    /// 迁移说明（MongoDB → PostgreSQL/EF Core）：
    /// <list type="bullet">
    /// <item><description>
    /// 原实现把 <c>UpdateDefinition&lt;T&gt;</c> / <c>FilterDefinition&lt;T&gt;</c> 通过
    /// <c>BsonSerializer</c> 渲染成 <see cref="MongoDB.Bson.BsonDocument"/>，
    /// 再断言 <c>"$set"</c> / <c>"$inc"</c> 的具体键值，甚至断言
    /// <c>set["FinishTime"].IsBsonNull</c> 这类 BSON 空值语义。
    /// 迁移后更新语义由 <see cref="UpdateSettersBuilder{T}"/> 表达，
    /// 因此改为<b>解析表达式树</b>：EF Core 10 的 <c>ExecuteUpdate</c> 收的是
    /// <c>Action&lt;UpdateSettersBuilder&lt;T&gt;&gt;</c> 委托，测试把该委托重放到一个
    /// 新建的 <see cref="UpdateSettersBuilder{T}"/> 上，再用
    /// <c>BuildSettersExpression()</c> 取回表达式树，逐层剥出 <c>SetProperty</c>
    /// 调用，得到「属性名 → 值」的映射。
    /// </description></item>
    /// <item><description>
    /// <c>UpdateResult.ModifiedCount</c> 变成 <c>Task&lt;int&gt;</c> 受影响行数，
    /// 于是 <c>TryMarkProcessingAsync_ReturnsFalseWhenStateNotAcquired</c> 用
    /// <c>AffectedRows = 0</c> 表达。
    /// </description></item>
    /// <item><description>
    /// <c>LastUpsert</c> 断言被删除：EF Core 的 <c>UpdateAsync(id, setters)</c> 没有
    /// upsert 参数，语义上恒为「按主键 UPDATE，不插入」。
    /// </description></item>
    /// <item><description>
    /// 原 <c>ClassInitialize</c> 里的 <c>BsonClassMap.TryRegisterClassMap</c> 已无必要——
    /// 不再有 BSON 序列化参与。
    /// </description></item>
    /// </list>
    /// </para>
    /// </summary>
    [TestClass]
    public class FormDataImportLogServiceStateMachineTests
    {
        private const string LogId = "log-1";

        [TestMethod]
        public async Task TryMarkProcessingAsync_RequiresPendingStatusAndRetryCount()
        {
            var repo = new RecordingRepository<FormDataImportLog>();
            var service = NewService(repo);

            var acquired = await service.TryMarkProcessingAsync(LogId, retryCount: 2);

            Assert.IsTrue(acquired);

            // 过滤谓词：Id == id && RetryCount == retryCount && Status == Pending
            var predicate = repo.LastPredicate!;
            var rendered = predicate.ToString();
            StringAssert.Contains(rendered, "Id");
            StringAssert.Contains(rendered, "RetryCount");
            StringAssert.Contains(rendered, "Status");
            Assert.IsFalse(rendered.Contains("OrElse"), "谓词应为 AND 组合，不应出现 OR。");
            Assert.IsFalse(rendered.Contains("ProcessingExpireTime"), "首次抢占不应带租约过期条件。");

            var setters = ReadSetters(repo.LastSetters!, () => new FormDataImportLog());
            Assert.AreEqual(FormDataImportStatus.Processing, setters[nameof(FormDataImportLog.Status)]);
            Assert.AreEqual(0L, setters[nameof(FormDataImportLog.TotalCount)]);
            Assert.IsTrue(setters.ContainsKey(nameof(FormDataImportLog.ProcessingExpireTime)));

            Assert.AreEqual(1, repo.AffectedRows);
        }

        [TestMethod]
        public async Task TryMarkProcessingAsync_ReturnsFalseWhenStateNotAcquired()
        {
            var repo = new RecordingRepository<FormDataImportLog> { AffectedRows = 0 };
            var service = NewService(repo);

            var acquired = await service.TryMarkProcessingAsync(LogId, retryCount: 2);

            Assert.IsFalse(acquired);
        }

        [TestMethod]
        public async Task MarkProcessingAsync_SetsStatusAndResetsCounters()
        {
            var repo = new RecordingRepository<FormDataImportLog>();
            var service = NewService(repo);

            await service.MarkProcessingAsync(LogId, totalCount: 100);

            var setters = ReadSetters(repo.LastSetters!, () => new FormDataImportLog());
            Assert.AreEqual(FormDataImportStatus.Processing, setters[nameof(FormDataImportLog.Status)]);
            Assert.AreEqual(100L, setters[nameof(FormDataImportLog.TotalCount)]);
            Assert.AreEqual(0L, setters[nameof(FormDataImportLog.ProcessedCount)]);
            Assert.AreEqual(0L, setters[nameof(FormDataImportLog.AddCount)]);
            Assert.AreEqual(0L, setters[nameof(FormDataImportLog.UpdateCount)]);
            Assert.AreEqual(0L, setters[nameof(FormDataImportLog.FailedCount)]);
            Assert.IsTrue(setters.ContainsKey(nameof(FormDataImportLog.StartTime)));
            Assert.IsNull(setters[nameof(FormDataImportLog.FinishTime)]);
            Assert.IsTrue(setters.ContainsKey(nameof(FormDataImportLog.ProcessingExpireTime)));
            Assert.IsNull(setters[nameof(FormDataImportLog.ErrorMessage)]);
        }

        [TestMethod]
        public async Task UpdateProgressAsync_OnlySetsCounters()
        {
            var repo = new RecordingRepository<FormDataImportLog>();
            var service = NewService(repo);

            await service.UpdateProgressAsync(LogId, processedCount: 20, addCount: 15, updateCount: 5, failedCount: 0);

            var setters = ReadSetters(repo.LastSetters!, () => new FormDataImportLog());
            Assert.AreEqual(20L, setters[nameof(FormDataImportLog.ProcessedCount)]);
            Assert.AreEqual(15L, setters[nameof(FormDataImportLog.AddCount)]);
            Assert.AreEqual(5L, setters[nameof(FormDataImportLog.UpdateCount)]);
            Assert.AreEqual(0L, setters[nameof(FormDataImportLog.FailedCount)]);
            Assert.IsTrue(setters.ContainsKey(nameof(FormDataImportLog.ProcessingExpireTime)));
            Assert.IsFalse(setters.ContainsKey(nameof(FormDataImportLog.Status)));
            Assert.IsFalse(setters.ContainsKey(nameof(FormDataImportLog.TotalCount)));
        }

        [TestMethod]
        public async Task MarkSucceededAsync_SetsTerminalStatusAndClearsEditableErrors()
        {
            var repo = new RecordingRepository<FormDataImportLog>();
            var service = NewService(repo);

            await service.MarkSucceededAsync(LogId, totalCount: 50, addCount: 30, updateCount: 20);

            var setters = ReadSetters(repo.LastSetters!, () => new FormDataImportLog());
            Assert.AreEqual(FormDataImportStatus.Succeeded, setters[nameof(FormDataImportLog.Status)]);
            Assert.AreEqual(50L, setters[nameof(FormDataImportLog.TotalCount)]);
            Assert.AreEqual(50L, setters[nameof(FormDataImportLog.ProcessedCount)]);
            Assert.AreEqual(30L, setters[nameof(FormDataImportLog.AddCount)]);
            Assert.AreEqual(20L, setters[nameof(FormDataImportLog.UpdateCount)]);
            Assert.AreEqual(0L, setters[nameof(FormDataImportLog.FailedCount)]);
            Assert.IsNull(setters[nameof(FormDataImportLog.EditableErrorRowsJson)]);
            Assert.IsNull(setters[nameof(FormDataImportLog.EditableErrorRowsObjectKey)]);
            Assert.AreEqual(0, setters[nameof(FormDataImportLog.EditableErrorRowCount)]);
            Assert.IsNull(setters[nameof(FormDataImportLog.ErrorMessage)]);
            Assert.IsTrue(setters.ContainsKey(nameof(FormDataImportLog.FinishTime)));
        }

        [TestMethod]
        public async Task MarkCompletedWithErrorsAsync_PersistsReportAndEditableRows()
        {
            var repo = new RecordingRepository<FormDataImportLog>();
            var service = NewService(repo);

            await service.MarkCompletedWithErrorsAsync(
                LogId,
                totalCount: 100, addCount: 80, updateCount: 10, failedCount: 10,
                errorReportFileName: "r.xlsx", errorReportObjectKey: "k", errorReportDownloadUrl: "https://x",
                editableErrorRowsJson: "[]", editableErrorRowsObjectKey: null, editableErrorRowCount: 5);

            var setters = ReadSetters(repo.LastSetters!, () => new FormDataImportLog());
            Assert.AreEqual(FormDataImportStatus.CompletedWithErrors, setters[nameof(FormDataImportLog.Status)]);
            Assert.AreEqual(100L, setters[nameof(FormDataImportLog.TotalCount)]);
            Assert.AreEqual(100L, setters[nameof(FormDataImportLog.ProcessedCount)]);
            Assert.AreEqual(80L, setters[nameof(FormDataImportLog.AddCount)]);
            Assert.AreEqual(10L, setters[nameof(FormDataImportLog.UpdateCount)]);
            Assert.AreEqual(10L, setters[nameof(FormDataImportLog.FailedCount)]);
            Assert.AreEqual("r.xlsx", setters[nameof(FormDataImportLog.ErrorReportFileName)]);
            Assert.AreEqual("k", setters[nameof(FormDataImportLog.ErrorReportObjectKey)]);
            Assert.AreEqual("https://x", setters[nameof(FormDataImportLog.ErrorReportDownloadUrl)]);
            Assert.AreEqual("[]", setters[nameof(FormDataImportLog.EditableErrorRowsJson)]);
            Assert.IsNull(setters[nameof(FormDataImportLog.EditableErrorRowsObjectKey)]);
            Assert.AreEqual(5, setters[nameof(FormDataImportLog.EditableErrorRowCount)]);
        }

        [TestMethod]
        public async Task MarkFailedAsync_SetsStatusErrorAndClearsEditableErrors()
        {
            var repo = new RecordingRepository<FormDataImportLog>();
            var service = NewService(repo);

            await service.MarkFailedAsync(LogId, "boom",
                errorReportFileName: "f.xlsx", errorReportObjectKey: "fk", errorReportDownloadUrl: "https://f");

            var setters = ReadSetters(repo.LastSetters!, () => new FormDataImportLog());
            Assert.AreEqual(FormDataImportStatus.Failed, setters[nameof(FormDataImportLog.Status)]);
            Assert.AreEqual("boom", setters[nameof(FormDataImportLog.ErrorMessage)]);
            Assert.AreEqual("f.xlsx", setters[nameof(FormDataImportLog.ErrorReportFileName)]);
            Assert.AreEqual("fk", setters[nameof(FormDataImportLog.ErrorReportObjectKey)]);
            Assert.AreEqual("https://f", setters[nameof(FormDataImportLog.ErrorReportDownloadUrl)]);
            Assert.IsNull(setters[nameof(FormDataImportLog.EditableErrorRowsJson)]);
            Assert.IsNull(setters[nameof(FormDataImportLog.EditableErrorRowsObjectKey)]);
            Assert.AreEqual(0, setters[nameof(FormDataImportLog.EditableErrorRowCount)]);
            Assert.IsTrue(setters.ContainsKey(nameof(FormDataImportLog.FinishTime)));
        }

        [TestMethod]
        public async Task MarkCorrectionResultAsync_WithErrors_PersistsRemainingEditableRowsAndClearsReport()
        {
            var repo = new RecordingRepository<FormDataImportLog>();
            var service = NewService(repo);

            await service.MarkCorrectionResultAsync(LogId, totalCount: 5, addCount: 2, updateCount: 1, failedCount: 2, editableErrorRowsJson: "[1,2]", editableErrorRowsObjectKey: null, editableErrorRowCount: 2);

            var setters = ReadSetters(repo.LastSetters!, () => new FormDataImportLog());
            Assert.AreEqual(FormDataImportStatus.CompletedWithErrors, setters[nameof(FormDataImportLog.Status)]);
            Assert.AreEqual(5L, setters[nameof(FormDataImportLog.TotalCount)]);
            Assert.AreEqual(5L, setters[nameof(FormDataImportLog.ProcessedCount)]);
            Assert.AreEqual(2L, setters[nameof(FormDataImportLog.AddCount)]);
            Assert.AreEqual(1L, setters[nameof(FormDataImportLog.UpdateCount)]);
            Assert.AreEqual(2L, setters[nameof(FormDataImportLog.FailedCount)]);
            Assert.AreEqual("[1,2]", setters[nameof(FormDataImportLog.EditableErrorRowsJson)]);
            Assert.IsNull(setters[nameof(FormDataImportLog.EditableErrorRowsObjectKey)]);
            Assert.AreEqual(2, setters[nameof(FormDataImportLog.EditableErrorRowCount)]);
            Assert.IsNull(setters[nameof(FormDataImportLog.ErrorReportFileName)]);
            Assert.IsNull(setters[nameof(FormDataImportLog.ErrorReportObjectKey)]);
            Assert.IsNull(setters[nameof(FormDataImportLog.ErrorReportDownloadUrl)]);
            Assert.IsNull(setters[nameof(FormDataImportLog.ErrorMessage)]);
            Assert.IsTrue(setters.ContainsKey(nameof(FormDataImportLog.FinishTime)));
        }

        [TestMethod]
        public async Task MarkCorrectionResultAsync_WithoutErrors_SetsSucceededAndClearsEditableRows()
        {
            var repo = new RecordingRepository<FormDataImportLog>();
            var service = NewService(repo);

            await service.MarkCorrectionResultAsync(LogId, totalCount: 3, addCount: 1, updateCount: 2, failedCount: 0, editableErrorRowsJson: null, editableErrorRowsObjectKey: null, editableErrorRowCount: 0);

            var setters = ReadSetters(repo.LastSetters!, () => new FormDataImportLog());
            Assert.AreEqual(FormDataImportStatus.Succeeded, setters[nameof(FormDataImportLog.Status)]);
            Assert.AreEqual(0, setters[nameof(FormDataImportLog.EditableErrorRowCount)]);
            Assert.IsNull(setters[nameof(FormDataImportLog.EditableErrorRowsJson)]);
            Assert.IsNull(setters[nameof(FormDataImportLog.ErrorReportDownloadUrl)]);
        }

        [TestMethod]
        public async Task UpdateEditableErrorsAsync_OnlyUpdatesEditableFields()
        {
            var repo = new RecordingRepository<FormDataImportLog>();
            var service = NewService(repo);

            await service.UpdateEditableErrorsAsync(LogId, "[1]", null, 3);

            var setters = ReadSetters(repo.LastSetters!, () => new FormDataImportLog());
            Assert.AreEqual("[1]", setters[nameof(FormDataImportLog.EditableErrorRowsJson)]);
            Assert.IsNull(setters[nameof(FormDataImportLog.EditableErrorRowsObjectKey)]);
            Assert.AreEqual(3, setters[nameof(FormDataImportLog.EditableErrorRowCount)]);
            Assert.IsFalse(setters.ContainsKey(nameof(FormDataImportLog.Status)));
        }

        [TestMethod]
        public async Task IncrementRetryAsync_OnlyIncrementsRetryCount()
        {
            var repo = new RecordingRepository<FormDataImportLog>();
            var service = NewService(repo);

            await service.IncrementRetryAsync(LogId);

            var setters = ReadSetters(repo.LastSetters!, () => new FormDataImportLog());
            Assert.AreEqual(1, setters.Count, "自增重试次数只应产生一个 SetProperty。");
            Assert.IsTrue(setters.ContainsKey(nameof(FormDataImportLog.RetryCount)));

            // EF Core 的 SetProperty 支持表达式自增，等价于 Mongo 的 $inc。
            // 记录下来的原始值是一个 Lambda（而非常量 1），据此确认语义确实是「读旧值 +1」。
            Assert.IsTrue(repo.LastRawIncrement, $"期望自增表达式，实际为: {repo.LastSetters}");
        }

        /// <summary>
        /// 从 <see cref="UpdateSettersBuilder{T}"/> 里剥出「属性名 → 值」映射。
        /// </summary>
        /// <remarks>
        /// <para>
        /// EF Core 10 的 setter 回调是委托（<c>Action&lt;UpdateSettersBuilder&lt;T&gt;&gt;</c>），
        /// 委托本身没有表达式树可解析；因此这里把委托<b>重放</b>到一个新建的
        /// <see cref="UpdateSettersBuilder{T}"/> 上，再取回 EF 组装好的表达式。
        /// </para>
        /// <para>
        /// 该表达式的形状是 <see cref="NewArrayExpression"/>：
        /// <c>new ITuple[] { new Tuple&lt;Delegate, object&gt;(属性 lambda, 值表达式), … }</c>。
        /// 逐个 <see cref="NewExpression"/> 取第 0 个实参作属性、第 1 个实参作值即可，
        /// 无需再展开 <c>SetProperty</c> 的调用链。
        /// </para>
        /// <para>
        /// 值表达式有两种形态：
        /// <list type="bullet">
        /// <item><description><c>SetProperty(property, value)</c>：常量表达式。</description></item>
        /// <item><description><c>SetProperty(property, valueExpression)</c>：<c>Func&lt;T, TValue&gt;</c>
        /// lambda（自增等场景），此处把它编译出来，对传入的空白实体求值即可。</description></item>
        /// </list>
        /// </para>
        /// </remarks>
        private static Dictionary<string, object?> ReadSetters(
            Action<UpdateSettersBuilder<FormDataImportLog>> setters,
            Func<FormDataImportLog> newEntity)
        {
            var result = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var setter in BuildSetters(setters).Expressions)
            {
                var tuple = (NewExpression)setter;
                if (UnwrapMember(tuple.Arguments[0]) is not { } member)
                {
                    continue;
                }

                result[member] = Evaluate(tuple.Arguments[1], newEntity);
            }

            return result;
        }

        /// <summary>
        /// 把上层记录的 setter <b>委托</b>重放一遍，换回 EF Core 组装好的 setter 数组表达式。
        /// </summary>
        /// <remarks>
        /// EF Core 10 的 <c>UpdateSettersBuilder&lt;T&gt;</c> 是一个可公开构造的记录型 builder
        /// （标记为 internal API）：把委托喂给它，它会记下每次 <c>SetProperty</c>，
        /// 再由 <c>BuildSettersExpression()</c> 还原成表达式树。
        /// 生产代码里的 <c>ExecuteUpdate</c> 走的也是同一条路径。
        /// </remarks>
        private static NewArrayExpression BuildSetters<T>(Action<UpdateSettersBuilder<T>> setters)
        {
            var builder = new UpdateSettersBuilder<T>();
            setters(builder);
            return builder.BuildSettersExpression();
        }

        /// <summary>
        /// 判断 setter 里是否含 <c>x =&gt; x.Prop + N</c> 这类表达式自增，等价于 Mongo 的 <c>$inc</c>。
        /// </summary>
        private static bool ContainsIncrement<T>(Action<UpdateSettersBuilder<T>> setters)
            => HasAdd(BuildSetters(setters));

        private static bool HasAdd(Expression expression) => expression switch
        {
            BinaryExpression { NodeType: ExpressionType.Add or ExpressionType.AddChecked } => true,
            UnaryExpression unary => HasAdd(unary.Operand),
            LambdaExpression lambda => HasAdd(lambda.Body),
            MethodCallExpression call => (call.Object is not null && HasAdd(call.Object)) || call.Arguments.Any(HasAdd),
            NewArrayExpression array => array.Expressions.Any(HasAdd),
            NewExpression @new => @new.Arguments.Any(HasAdd),
            _ => false,
        };

        private static string? UnwrapMember(Expression expression)
        {
            // setter 数组元素的第 0 个实参是 Lambda: x => x.Property
            if (expression is UnaryExpression unary)
            {
                return UnwrapMember(unary.Operand);
            }

            if (expression is LambdaExpression lambda)
            {
                return UnwrapMember(lambda.Body);
            }

            return (expression as MemberExpression)?.Member.Name;
        }

        private static object? Evaluate(Expression expression, Func<FormDataImportLog> newEntity)
        {
            if (expression is UnaryExpression unary)
            {
                return Evaluate(unary.Operand, newEntity);
            }

            if (expression is LambdaExpression lambda)
            {
                // x => x.RetryCount + 1 —— 对空白实体求值等价于「旧值 0 + 1」。
                var compiled = lambda.Compile();
                return compiled.DynamicInvoke(newEntity());
            }

            if (expression is ConstantExpression constant)
            {
                return constant.Value;
            }

            // 闭包字段（如本地变量）访问：编译后取值。
            return Expression.Lambda(expression).Compile().DynamicInvoke();
        }

        private static IFormDataImportLogService NewService(IRepository<FormDataImportLog> repo)
        {
            var resolver = new TestResolver(repo);
            return new FormDataImportLogService(resolver);
        }

        private sealed class TestResolver : IResolver
        {
            private readonly Dictionary<Type, object> _services = new();

            public TestResolver(IRepository<FormDataImportLog> repo)
            {
                _services[typeof(IRepository<FormDataImportLog>)] = repo;
                _services[typeof(IRepository<AuditLog>)] = new StubRepository<AuditLog>();
                _services[typeof(ICacheClient)] = new FakeCacheClient();
                _services[typeof(IServiceContext)] = new FakeServiceContext();
                _services[typeof(IScopeCache)] = new FakeScopeCache();
                _services[typeof(ILogger<FormDataImportLog>)] = NullLogger<FormDataImportLog>.Instance;
            }

            public CompositionContainer MefContainer => throw new NotSupportedException();
            public object Resolve(Type type, string? name = null) => _services[type];
            public T Resolve<T>(string? name = null) where T : class => (T)_services[typeof(T)];
            public T GetExport<T>(string? name = null) where T : class => Resolve<T>(name);
            public object GetExport(Type type, string? name = null) => Resolve(type, name);
            public IEnumerable<T> GetExports<T>(string? name = null) where T : class => [Resolve<T>(name)];
            public IEnumerable<object> GetExports(Type type, string? name = null) => [Resolve(type, name)];
        }

        private sealed class FakeServiceContext : IServiceContext
        {
            public string AccessToken { get; set; } = string.Empty;
            public string CorpId { get; set; } = string.Empty;
            public Operator? Operator { get; set; }
            public string UserId { get; set; } = string.Empty;
            public IUser? User { get; set; }
            public IEmployee? Employee { get; set; }
            public string? ClientIp { get; set; }
            public DataAction Action { get; set; }
            public IScopeCache ScopeCache { get; set; } = new FakeScopeCache();
        }

        private sealed class FakeScopeCache : IScopeCache
        {
            public IEnumerable<T> GetAll<T>(DataVersion version = DataVersion.Temp) where T : class => [];
            public T? Get<T>(string key, DataVersion version = DataVersion.Temp, Func<string, T?>? getter = null) where T : class => null;
            public void Set<T>(string key, T value, DataVersion version = DataVersion.Temp) where T : class { }
            public void Remove<T>(string key, DataVersion version = DataVersion.Temp) where T : class { }
            public bool Contains<T>(string key, DataVersion version = DataVersion.Temp) where T : class => false;
        }

        private sealed class FakeCacheClient : ICacheClient
        {
            public string? GetString(string key, CacheScope scope, string scopeId = "") => null;
            public Task<string?> GetStringAsync(string key, CacheScope scope, string scopeId = "") => Task.FromResult<string?>(null);
            public T? Get<T>(string key, CacheScope scope, string scopeId = "") => default;
            public Task<T?> GetAsync<T>(string key, CacheScope scope, string scopeId = "") => Task.FromResult<T?>(default);
            public void SetString(string key, string value, CacheScope scope, string scopeId = "", DistributedCacheEntryOptions? options = null) { }
            public Task SetStringAsync(string key, string value, CacheScope scope, string scopeId = "", DistributedCacheEntryOptions? options = null) => Task.CompletedTask;
            public void Set<T>(string key, T value, CacheScope scope, string scopeId = "", DistributedCacheEntryOptions? options = null) { }
            public Task SetAsync<T>(string key, T value, CacheScope scope, string scopeId = "", DistributedCacheEntryOptions? options = null) => Task.CompletedTask;
            public void Refresh(string key, CacheScope scope, string scopeId = "") { }
            public Task RefreshAsync(string key, CacheScope scope, string scopeId = "") => Task.CompletedTask;
            public void Remove(string key, CacheScope scope, string scopeId = "") { }
            public Task RemoveAsync(string key, CacheScope scope, string scopeId = "") => Task.CompletedTask;
            public long Increment(string key, long delta, TimeSpan ttl, CacheScope scope, string scopeId = "") => 0;
            public Task<long> IncrementAsync(string key, long delta, TimeSpan ttl, CacheScope scope, string scopeId = "") => Task.FromResult(0L);
            public Task<bool> TrySetStringAsync(string key, string value, TimeSpan ttl, CacheScope scope, string scopeId = "") => Task.FromResult(true);
        }

        /// <summary>
        /// 记录型仓储，捕获 EF Core 更新调用的谓词与 setter 表达式树。
        /// <para>
        /// 迁移说明：原实现捕获并渲染 <c>UpdateDefinition</c> / <c>FilterDefinition</c>，
        /// 用 <c>UpdateResult.Acknowledged(1, ModifiedCount, new BsonDocument())</c> 返回结果。
        /// 现在改为捕获 <see cref="Expression{TDelegate}"/> 并返回 <c>int</c> 受影响行数。
        /// </para>
        /// </summary>
        private sealed class RecordingRepository<T> : StubRepository<T> where T : class, IEntityKey
        {
            public Expression<Func<T, bool>>? LastPredicate { get; private set; }
            public Action<UpdateSettersBuilder<T>>? LastSetters { get; private set; }
            public string? LastUpdateId { get; private set; }
            public int AffectedRows { get; set; } = 1;

            /// <summary>供 <c>IncrementRetryAsync</c> 之类的自增断言使用。</summary>
            public bool LastRawIncrement { get; private set; }

            public override Task<int> UpdateAsync(
                string id,
                Action<UpdateSettersBuilder<T>> setters,
                CancellationToken cancellationToken = default)
            {
                LastSetters = setters;
                LastUpdateId = id;
                LastRawIncrement = ContainsIncrement(setters);
                return Task.FromResult(AffectedRows);
            }

            public override Task<int> UpdateManyAsync(
                Expression<Func<T, bool>> predicate,
                Action<UpdateSettersBuilder<T>> setters,
                CancellationToken cancellationToken = default)
            {
                LastPredicate = predicate;
                LastSetters = setters;
                LastRawIncrement = ContainsIncrement(setters);
                return Task.FromResult(AffectedRows);
            }
        }
    }
}
