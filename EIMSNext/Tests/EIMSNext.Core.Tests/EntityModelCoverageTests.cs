using EIMSNext.Core.Abstractions;

using Microsoft.EntityFrameworkCore;

namespace EIMSNext.Core.Tests
{
    /// <summary>
    /// 守门用例：<c>EIMSNext.Entities</c> 里的每个实体都必须登记进 <see cref="EIMSNext.Persistence.PostgreSql.PostgreSqlDbContext"/>。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 背景：<c>DbRepository&lt;T&gt;</c> 一律走 <c>Context.Set&lt;T&gt;()</c>，实体只要没登记进模型，
    /// 业务第一次解析仓储就会抛
    /// <c>Cannot create a DbSet for 'X' because this type is not included in the model for the context</c>。
    /// 编译期完全看不出来：仓储是泛型解析出来的，没有静态引用。
    /// </para>
    /// <para>
    /// 这一轮迁移里正是靠这条规则捞出了 20 个「业务在用、模型里没有」的实体
    /// （<c>AppTemplate</c>、<c>AuditLog</c>、<c>PluginInstall</c>、<c>ECoinPrice</c>、
    /// <c>UploadedFile</c>、<c>WebPushLog</c>、<c>DashboardTemplate</c> 等），
    /// 属于「编译通过、一连库就崩」级缺口。原先是靠一个一次性 Python 脚本扫源码得到的，
    /// 现在固化成运行时断言，避免以后再漏。
    /// </para>
    /// <para>
    /// 有意排除的：
    /// <list type="bullet">
    /// <item><description>抽象基类与接口本身；</description></item>
    /// <item><description>不作为独立表存在的值对象——它们没有继承 <c>IEntity</c>，天然不在集合里；</description></item>
    /// <item><description>显式被 <c>Ignore()</c> 的类型（用模型里的忽略集合判断）。</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    [TestClass]
    public sealed class EntityModelCoverageTests
    {
        [TestMethod]
        public void EveryEntityTypeIsMappedInPostgreSqlDbContext()
        {
            using var db = TestDbFactory.Create();
            db.Model.GetEntityTypes(); // 触发模型构建，配置错误在这里就会暴露

            var assembly = typeof(EIMSNext.Entities.FormData).Assembly;

            var unmapped = assembly
                .GetTypes()
                .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
                .Where(type => typeof(IEntity).IsAssignableFrom(type))
                .Where(type => db.Model.FindEntityType(type) is null)
                .Select(type => type.FullName ?? type.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();

            if (unmapped.Count == 0) return;

            Assert.Fail(
                $"EIMSNext.Entities 里有 {unmapped.Count} 个实体没有登记进 PostgreSqlDbContext。" +
                Environment.NewLine +
                "未登记的类型会在业务解析仓储时抛 “Cannot create a DbSet for …”，必须补 DbSet 与 jsonb 映射。" +
                Environment.NewLine +
                string.Join(Environment.NewLine, unmapped));
        }

        /// <summary>确认本守门用例真的看到了实体，避免程序集名写错导致「空集合恒通过」。</summary>
        [TestMethod]
        public void EntitiesAssemblyIsDiscovered()
        {
            var assembly = typeof(EIMSNext.Entities.FormData).Assembly;
            var count = assembly.GetTypes()
                .Count(type => type is { IsClass: true, IsAbstract: false }
                    && typeof(IEntity).IsAssignableFrom(type));

            Assert.IsTrue(count > 50, $"EIMSNext.Entities 里只发现 {count} 个实体，程序集解析可能有问题。");
        }
    }
}
