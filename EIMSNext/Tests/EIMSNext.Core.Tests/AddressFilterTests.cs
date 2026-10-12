using System.Dynamic;

using EIMSNext.Common;
using EIMSNext.Core.Query;
using EIMSNext.Entities;
using EIMSNext.Persistence.PostgreSql;

using Microsoft.EntityFrameworkCore;

using MSTest = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EIMSNext.Core.Tests;

// 注意：本测试命名空间下有同名夹具类 EIMSNext.Core.Tests.FormData（见 DynamicData.cs），
// 其优先级高于 using 别名，因此下面的实体类型一律用 EIMSNext.Entities.FormData 全限定。

/// <summary>
/// address（地址）字段过滤：
/// 值为 jsonb 对象 <c>{"province":"北京市","city":"北京市","district":"东城区","detail":".."}</c>，
/// 过滤值是 "省/市/区" 前缀字符串，语义是「逐级等值匹配」（省→province、市→city、区→district）。
/// 每级翻译成一条 <c>strict $.f_addr.province ? (@ == "..")</c> 的 @? 谓词，
/// 同前缀多级 AND、多前缀 OR，经 <c>@?</c> 命中 <c>IX_FormData_Data_Gin</c>。
/// </summary>
[TestClass]
public sealed class AddressFilterTests
{
    private static Dictionary<string, object?> Address(string province, string city, string district, string detail = "") =>
        new() { ["province"] = province, ["city"] = city, ["district"] = district, ["detail"] = detail };

    private static List<EIMSNext.Entities.FormData> SeedRows() =>
    [
        new EIMSNext.Entities.FormData
        {
            Id = "addr-1",
            CorpId = "addr-probe-corp",
            Data = new Dictionary<string, object?> { ["f_addr"] = Address("北京市", "北京市", "东城区", "某某路1号") },
        },
        new EIMSNext.Entities.FormData
        {
            Id = "addr-2",
            CorpId = "addr-probe-corp",
            Data = new Dictionary<string, object?> { ["f_addr"] = Address("北京市", "北京市", "海淀区") },
        },
        new EIMSNext.Entities.FormData
        {
            Id = "addr-3",
            CorpId = "addr-probe-corp",
            Data = new Dictionary<string, object?> { ["f_addr"] = Address("河北省", "石家庄市", "长安区") },
        },
        // 干扰项：字段名相同但值是标量脏数据。strict 路径在 @? 下静默不命中。
        new EIMSNext.Entities.FormData
        {
            Id = "addr-4",
            CorpId = "addr-probe-corp",
            Data = new Dictionary<string, object?> { ["f_addr"] = "北京市" },
        },
        // 干扰项：地址为空。
        new EIMSNext.Entities.FormData
        {
            Id = "addr-5",
            CorpId = "addr-probe-corp",
            Data = new Dictionary<string, object?> { ["f_name"] = "x" },
        },
    ];

    private static async Task<List<string>> QueryIdsAsync(PostgreSqlDbContext db, DynamicFilter filter)
    {
        return await db.Set<EIMSNext.Entities.FormData>()
            .IgnoreQueryFilters()
            .Where(filter.ToPredicate<EIMSNext.Entities.FormData>())
            .Where(f => f.CorpId == "addr-probe-corp")
            .Select(f => f.Id)
            .ToListAsync();
    }

    [MSTest.TestMethod]
    public async Task Address_In_ProvincePrefix_MatchesAllInProvince()
    {
        await using var db = TestDbFactory.Create();
        await db.Database.OpenConnectionAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Set<EIMSNext.Entities.FormData>().AddRange(SeedRows());
        await db.SaveChangesAsync();

        var filter = new DynamicFilter
        {
            Field = "data.f_addr",
            Type = FieldType.Address,
            Op = FilterOp.In,
            Value = "北京市",
        };

        var ids = await QueryIdsAsync(db, filter);
        CollectionAssert.AreEquivalent(new[] { "addr-1", "addr-2" }, ids);

        await transaction.RollbackAsync();
    }

    [MSTest.TestMethod]
    public async Task Address_In_CityPrefix_MatchesByLevel()
    {
        await using var db = TestDbFactory.Create();
        await db.Database.OpenConnectionAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Set<EIMSNext.Entities.FormData>().AddRange(SeedRows());
        await db.SaveChangesAsync();

        var filter = new DynamicFilter
        {
            Field = "data.f_addr",
            Type = FieldType.Address,
            Op = FilterOp.In,
            Value = "北京市/北京市/海淀区",
        };

        var ids = await QueryIdsAsync(db, filter);
        CollectionAssert.AreEquivalent(new[] { "addr-2" }, ids);

        await transaction.RollbackAsync();
    }

    [MSTest.TestMethod]
    public async Task Address_In_MultiplePrefixes_OrSemantics()
    {
        await using var db = TestDbFactory.Create();
        await db.Database.OpenConnectionAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Set<EIMSNext.Entities.FormData>().AddRange(SeedRows());
        await db.SaveChangesAsync();

        var filter = new DynamicFilter
        {
            Field = "data.f_addr",
            Type = FieldType.Address,
            Op = FilterOp.In,
            Value = new[] { "河北省", "北京市/北京市/东城区" },
        };

        var ids = await QueryIdsAsync(db, filter);
        CollectionAssert.AreEquivalent(new[] { "addr-1", "addr-3" }, ids);

        await transaction.RollbackAsync();
    }

    [MSTest.TestMethod]
    public async Task Address_Nin_ExcludesPrefixedRows()
    {
        await using var db = TestDbFactory.Create();
        await db.Database.OpenConnectionAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Set<EIMSNext.Entities.FormData>().AddRange(SeedRows());
        await db.SaveChangesAsync();

        var filter = new DynamicFilter
        {
            Field = "data.f_addr",
            Type = FieldType.Address,
            Op = FilterOp.Nin,
            Value = "北京市",
        };

        var ids = await QueryIdsAsync(db, filter);
        // addr-4 的地址是标量脏数据（合法数据只会是对象），键存在且不命中前缀，
        // 按 nin 语义（键存在 且 不命中任何前缀）一并返回。
        CollectionAssert.AreEquivalent(new[] { "addr-3", "addr-4" }, ids);

        await transaction.RollbackAsync();
    }

    [MSTest.TestMethod]
    public async Task Address_Empty_NotEmpty()
    {
        await using var db = TestDbFactory.Create();
        await db.Database.OpenConnectionAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Set<EIMSNext.Entities.FormData>().AddRange(SeedRows());
        await db.SaveChangesAsync();

        var empty = new DynamicFilter { Field = "data.f_addr", Type = FieldType.Address, Op = FilterOp.Empty };
        var emptyIds = await QueryIdsAsync(db, empty);
        CollectionAssert.AreEquivalent(new[] { "addr-5" }, emptyIds);

        var notEmpty = new DynamicFilter { Field = "data.f_addr", Type = FieldType.Address, Op = FilterOp.NotEmpty };
        var notEmptyIds = await QueryIdsAsync(db, notEmpty);
        CollectionAssert.AreEquivalent(new[] { "addr-1", "addr-2", "addr-3", "addr-4" }, notEmptyIds);

        await transaction.RollbackAsync();
    }

    [MSTest.TestMethod]
    public async Task Address_UsesGinIndex()
    {
        await using var db = TestDbFactory.Create();
        await db.Database.OpenConnectionAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();

        // 必须：① 事务里有行，② 下面的 set local 在事务内才有效。
        db.Set<EIMSNext.Entities.FormData>().AddRange(SeedRows());
        await db.SaveChangesAsync();

        var filter = new DynamicFilter
        {
            Field = "data.f_addr",
            Type = FieldType.Address,
            Op = FilterOp.In,
            Value = "北京市/北京市",
        };
        var sql = db.Set<EIMSNext.Entities.FormData>()
            .IgnoreQueryFilters()
            .Where(filter.ToPredicate<EIMSNext.Entities.FormData>())
            .Select(f => f.Id)
            .ToQueryString();

        MSTest.Assert.IsTrue(sql.Contains("@?"), $"应生成 @? 运算符，实际 SQL：\n{sql}");

        var plan = await ExplainAsync(db, sql);
        MSTest.Assert.IsTrue(plan.Contains("IX_FormData_Data_Gin"),
            $"执行计划应命中 GIN 索引。SQL：\n{sql}\n执行计划：\n{plan}");

        await transaction.RollbackAsync();
    }

    private static async Task<string> ExplainAsync(PostgreSqlDbContext db, string sql)
    {
        var connection = (Npgsql.NpgsqlConnection)db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();

        command.CommandText = """
            set local enable_seqscan = off;
            set local enable_indexscan = off;
            set local enable_indexonlyscan = off;
            """;
        await command.ExecuteNonQueryAsync();

        command.CommandText = "explain (costs off) " + sql;
        var lines = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) lines.Add(reader.GetString(0));
        return string.Join("\n", lines);
    }
}
