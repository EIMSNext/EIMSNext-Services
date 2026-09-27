using EIMSNext.Persistence.PostgreSql;

using Microsoft.EntityFrameworkCore;

namespace EIMSNext.Core.Tests
{
    /// <summary>
    /// 测试专用上下文，只装载本测试工程自有实体（<see cref="FormData"/>、<see cref="EntityData"/>）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 背景：这些测试要验证 <c>DbRepository&lt;T&gt;</c> 与 <c>DynamicFilter</c> → EF Core 谓词的翻译，
    /// 而 <c>DbRepository&lt;T&gt;</c> 一律走 <c>Context.Set&lt;T&gt;()</c>，实体必须在模型里，
    /// 否则访问即抛 <c>Cannot create a DbSet for 'X' because this type is not included in the model</c>。
    /// 生产上下文 <see cref="PostgreSqlDbContext"/> 是 sealed 且不该认识测试实体，因此这里单开一个。
    /// </para>
    /// <para>
    /// <b>表名</b>：测试实体 <c>EIMSNext.Core.Tests.FormData</c> 与业务实体
    /// <c>EIMSNext.Entities.FormData</c> 类名相同，而 <see cref="EIMSNextModelConfiguration.ApplyEIMSNextModel"/>
    /// 的约定是「表名 = 类名」，直接沿用会撞上真实业务表。因此这里显式改名为
    /// <c>TestFormData</c> / <c>TestEntityData</c>。这两张表只存在于测试库，
    /// 由 <see cref="TestDbFactory.EnsureTestSchema"/> 按本上下文模型生成，
    /// <b>不进入</b> <c>EIMSNext.Tool.DbMaintenance</c> 的迁移链。
    /// </para>
    /// </remarks>
    public sealed class TestPostgreSqlDbContext(DbContextOptions<TestPostgreSqlDbContext> options) : DbContext(options)
    {
        /// <summary>测试用表单数据。</summary>
        public DbSet<FormData> TestFormDatas => Set<FormData>();

        /// <summary>测试用普通实体。</summary>
        public DbSet<EntityData> EntityDatas => Set<EntityData>();

        /// <summary>测试用表单定义（用于验证 FormContent 这一 jsonb 强类型对象的往返）。</summary>
        public DbSet<EIMSNext.Entities.FormDef> TestFormDefs => Set<EIMSNext.Entities.FormDef>();

        /// <inheritdoc />
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder.ConfigureEIMSNextConventions();

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 先应用共享约定（它会把表名统一改成类名），再覆盖成测试专用表名，顺序不能反。
            modelBuilder.ApplyEIMSNextModel();

            modelBuilder.Entity<FormData>().ToTable("TestFormData");
            modelBuilder.Entity<EntityData>().ToTable("TestEntityData");
            modelBuilder.Entity<EIMSNext.Entities.FormDef>().ToTable("TestFormDef");
            // 测试表不能复用生产 FormDef 的索引名（IX_FormDef_CorpId_AppId 在测试库里已存在，
            // PostgreSQL 要求索引名在 schema 内唯一），否则 EnsureTestSchema 建表会报“已存在”。
            modelBuilder.Entity<EIMSNext.Entities.FormDef>()
                .HasIndex(x => new { x.CorpId, x.AppId })
                .IsUnique()
                .HasDatabaseName("IX_TestFormDef_CorpId_AppId");

            // Data 是 Dictionary<string, object?>（原 ExpandoObject），Fields 是 List<FieldDef>：都要落 jsonb 单列。
            // 共享约定里的 Mapped<FormData> 指向的是业务实体 FormData，对测试实体不生效，
            // 所以这两个属性在这里单独声明。
            modelBuilder.Entity<FormData>().Property(x => x.Data)
                .HasConversion(new DynamicJsonbValueConverter())
                .HasColumnType("jsonb");
            modelBuilder.Entity<EntityData>()
                .Property(x => x.Fields)
                .HasConversion(JsonbValueConverter.Create<FieldDefList>())
                .HasColumnType("jsonb");
        }
    }
}
