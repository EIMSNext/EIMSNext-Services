using EIMSNext.Entities;
using EIMSNext.Identity.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EIMSNext.Identity.Tests;

/// <summary>
/// 身份宿主上下文的模型契约。
/// <para>
/// 这里刻意不连数据库：模型校验（内嵌集合/对象是否都挂了值转换器、表名是否正确）
/// 在构建模型阶段就能判定，而它正是最容易漏、且一旦漏掉就会让整个宿主一启动就崩的地方——
/// <c>IdentityDbContext</c> 原先只写了 <c>ToTable</c>，没有 jsonb 转换器，
/// <c>Client.ClientSecrets</c> 等属性会被 EF 当成独立实体去要主键，
/// 抛 <c>The entity type 'ClientSecret' requires a primary key to be defined</c>。
/// 触碰 <c>db.Model</c> 即可复现该失败。
/// </para>
/// </summary>
[TestClass]
public sealed class IdentityDbContextModelTests
{
    private static IdentityDbContext CreateContext()
    {
        // 连接串不会被使用：只构建模型，不打开连接。
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql("Host=localhost;Database=EIMSNextModelOnly;Username=postgres;Password=postgres")
            .Options;
        return new IdentityDbContext(options);
    }

    [TestMethod]
    public void ModelBuildsAndMapsExpectedTables()
    {
        using var db = CreateContext();

        // 视图（CorporateSettingReadModel → CorporateSetting）没有表名，只有视图名，
        // 因此两者都收进来。
        var detail = db.Model.GetEntityTypes()
            .Select(x => $"{x.ClrType.Name}(table={x.GetTableName() ?? "-"}, view={x.GetViewName() ?? "-"})")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        var mapped = db.Model.GetEntityTypes()
            .SelectMany(x => new[] { x.GetTableName(), x.GetViewName() })
            .Where(x => x is not null)
            .Select(x => x!)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var expected in new[] { "Client", "User", "Employee", "IdentityLoginAudit", "PublicSetting", "CorporateSetting" })
        {
            Assert.Contains(expected, mapped,
                $"身份宿主的模型缺少 \"{expected}\"。实际映射：{string.Join("; ", detail)}");
        }
    }

    [TestMethod]
    public void NestedCollectionsAreMappedAsJsonbNotEntities()
    {
        using var db = CreateContext();

        // 这些类型必须作为 jsonb 值转换器存在于宿主属性上，而不是作为实体类型。
        // 一旦变成实体类型，说明对应的值转换器没挂上（EF 会给它们加影子主键而不报错，
        // 于是同一份数据被理解成额外的表）。
        foreach (var nested in new[]
                 {
                     typeof(ClientSecret), typeof(ClientGrantType), typeof(ClientScope)
                 })
        {
            Assert.IsNull(db.Model.FindEntityType(nested),
                $"{nested.Name} 不应被映射为实体类型；请检查其值转换器是否已挂到宿主属性上。");
        }

        // UserCorp 相反：它是用户与企业归属的关系表，身份宿主签发 token 时需要读取它，
        // 因此必须显式映射为实体类型（原 jsonb 投影 User.Crops 已删除）。
        Assert.IsNotNull(db.Model.FindEntityType(typeof(UserCorp)),
            "UserCorp 应作为关系表实体类型映射到身份宿主的模型中。");

        var client = db.Model.FindEntityType(typeof(Client));
        Assert.IsNotNull(client);

        foreach (var column in new[] { "ClientSecrets", "AllowedGrantTypes", "AllowedScopes" })
        {
            var property = client.FindProperty(column);
            Assert.IsNotNull(property, $"Client 缺少属性 {column}。");
            Assert.AreEqual("jsonb", property.GetColumnType(), $"Client.{column} 的列类型应为 jsonb。");
            Assert.IsNotNull(property.GetValueConverter(), $"Client.{column} 缺少值转换器。");
        }
    }
}
