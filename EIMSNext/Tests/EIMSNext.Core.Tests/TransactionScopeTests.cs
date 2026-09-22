using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Persistence.PostgreSql;
using EIMSNext.Entities;

using Microsoft.EntityFrameworkCore;

namespace EIMSNext.Core.Tests
{
    /// <summary>
    /// 事务作用域行为测试。
    /// <para>
    /// 迁移说明：原文件名为 <c>MongoTransactionScopeTest</c>，被测对象是 <c>MongoTransactionScope</c>。
    /// 现已改为被测 EF Core 版 <see cref="TransactionScope"/>，并同步调整了断言：
    /// <list type="bullet">
    /// <item><description><c>scope.SessionHandle</c> / <c>Find(...).CountDocuments()</c>
    /// 已随 Mongo API 移除，改为 <see cref="TransactionScope.Transaction"/> 与 LINQ <c>Count()</c>。</description></item>
    /// <item><description><c>ExecuteWithRetry</c>（同步）已删除，只保留
    /// <see cref="TransactionScope.ExecuteWithRetryAsync{TResult}"/>，对应测试合并为异步版本。</description></item>
    /// </list>
    /// </para>
    /// </summary>
    [TestClass]
    public class TransactionScopeTests
    {
        protected PostgreSqlDbContext? _dbContext;

        [TestMethod]
        public void ThreadTransactionTest()
        {
            using (var scope = new TransactionScope(_dbContext!))
            {
                using (ExecutionContext.SuppressFlow())
                {
                    Task.Run(() =>
                    {
                        var t = TransactionScope.Transaction;
                        Assert.IsNull(t);
                    });
                }

                Thread.Sleep(1000);

                Assert.IsTrue(TransactionScope.IsInTransaction);
                Thread.Sleep(1000);
                Assert.IsTrue(TransactionScope.IsInTransaction);
            }

            Assert.IsNull(TransactionScope.Transaction);
        }

        [TestMethod]
        public void AutoInTransactionTest()
        {
            using (var scope = new TransactionScope(_dbContext!))
            {
                _dbContext!.FormDatas.Add(new EIMSNext.Entities.FormData
                {
                    Id = Guid.NewGuid().ToString("N"),
                    AppId = "111",
                    FormId = "form_1",
                    CorpId = "corp_1",
                    CreateTime = DateTime.Today.ToTimeStampMs(),
                });
                _dbContext.SaveChanges();

                var today = DateTime.Today.ToTimeStampMs();
                var count = _dbContext.FormDatas.Count(x => x.CreateTime >= today);
                Assert.AreEqual(1, count);

                scope.CommitTransaction();

                // 提交后同一上下文仍可查询（EF Core 的事务绑在连接上，无 Mongo 的会话限制）。
                Assert.AreEqual(1, _dbContext.FormDatas.Count(x => x.CreateTime >= today));
            }
        }

        [TestMethod]
        public void DisabledRootScopeDoesNotStartTransaction()
        {
            using (var scope = new TransactionScope(_dbContext!, enabled: false))
            {
                Assert.IsNull(TransactionScope.Transaction);
                Assert.IsFalse(TransactionScope.IsInTransaction);
            }

            Assert.IsNull(TransactionScope.Transaction);
        }

        [TestMethod]
        public void NestedScopeInheritsEnabledRootTransaction()
        {
            using (var root = new TransactionScope(_dbContext!, enabled: true))
            {
                var rootTransaction = TransactionScope.Transaction;
                Assert.IsNotNull(rootTransaction);
                Assert.IsTrue(TransactionScope.IsInTransaction);

                using (var nested = new TransactionScope(_dbContext!, enabled: false))
                {
                    // 内层复用外层事务，不新开。
                    Assert.AreSame(rootTransaction, TransactionScope.Transaction);
                    Assert.IsTrue(TransactionScope.IsInTransaction);
                }

                Assert.AreSame(rootTransaction, TransactionScope.Transaction);
            }
        }

        [TestMethod]
        public void DisabledRootRunsAfterCommitImmediately()
        {
            var called = false;
            using (var scope = new TransactionScope(_dbContext!, enabled: false))
            {
                TransactionScope.RegisterAfterCommit(() =>
                {
                    called = true;
                    return Task.CompletedTask;
                });

                Assert.IsTrue(called);
            }
        }

        [TestMethod]
        public void EnabledRootRunsAfterCommitOnlyAfterCommit()
        {
            var called = false;
            using (var scope = new TransactionScope(_dbContext!, enabled: true))
            {
                TransactionScope.RegisterAfterCommit(() =>
                {
                    called = true;
                    return Task.CompletedTask;
                });

                Assert.IsFalse(called);
                scope.CommitTransaction();
                // 回调在释放阶段（Dispose）执行，提交本身不触发。
                Assert.IsFalse(called);
            }

            Assert.IsTrue(called);
        }

        [TestMethod]
        public void SuppressAmbientTemporarilyHidesAndRestoresRootTransaction()
        {
            using var root = new TransactionScope(_dbContext!);
            var transaction = TransactionScope.Transaction;
            Assert.IsNotNull(transaction);

            using (TransactionScope.SuppressAmbient())
            {
                Assert.IsNull(TransactionScope.Transaction);
                Assert.IsFalse(TransactionScope.IsInTransaction);
            }

            Assert.AreSame(transaction, TransactionScope.Transaction);
            Assert.IsTrue(TransactionScope.IsInTransaction);
        }

        [TestMethod]
        public async Task RetryExecutorReusesAmbientTransaction()
        {
            await using var root = new TransactionScope(_dbContext!);
            var calls = 0;
            var result = await TransactionScope.ExecuteWithRetryAsync(
                _dbContext!,
                () =>
                {
                    calls++;
                    // 已处于事务中时直接执行，不重复开启。
                    Assert.IsTrue(TransactionScope.IsInTransaction);
                    return Task.FromResult(42);
                }, maxRetries: 1);

            Assert.AreEqual(42, result);
            Assert.AreEqual(1, calls);
        }

        [TestMethod]
        public async Task RetryExecutorDoesNotRetryNonTransientErrors()
        {
            var calls = 0;
            try
            {
                await TransactionScope.ExecuteWithRetryAsync<int>(_dbContext!, () =>
                {
                    calls++;
                    throw new InvalidOperationException("expected");
                }, maxRetries: 1);
                Assert.Fail();
            }
            catch (InvalidOperationException) { }

            Assert.AreEqual(1, calls);
        }

        [TestMethod]
        public async Task RetryExecutorRejectsNegativeRetryCount()
        {
            // 契约与原 MongoTransactionScope 一致：负的重试次数属于调用方错误，
            // 由 ExecuteWithRetryAsync 入口直接拒绝（ArgumentOutOfRangeException）。
            // 若缺少该校验，for 循环一次都不进入，最终会抛出 NullReferenceException。
            var ex = await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(async () =>
            {
                await TransactionScope.ExecuteWithRetryAsync<int>(_dbContext!, () =>
                    throw new InvalidOperationException("boom"), maxRetries: -1);
            });

            Assert.AreEqual("maxRetries", ex.ParamName);
        }

        [TestInitialize]
        public void Init()
        {
            _dbContext = TestDbFactory.Create();
            _dbContext.FormDatas.IgnoreQueryFilters().ExecuteDelete();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _dbContext?.Dispose();
        }
    }
}
