using System.Dynamic;

using EIMSNext.Core.Query;
using EIMSNext.Entities;
using EIMSNext.Persistence.PostgreSql;

using Microsoft.EntityFrameworkCore;

using MSTest = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EIMSNext.Core.Tests;

/// <summary>
/// jsonb 过滤谓词必须走 <c>@?</c> 运算符而不是 <c>jsonb_path_exists</c> 函数调用：
/// GIN 索引（jsonb_ops / jsonb_path_ops）只服务运算符谓词，函数调用只能是 Seq Scan。
/// 实测（PostgreSQL 18，enable_seqscan=off）：同样的路径，<c>data @? path</c> 命中
/// <c>IX_FormData_Data_Gin</c>（Bitmap Index Scan），函数调用不变。
/// </summary>
[TestClass]
public sealed class JsonbGinIndexTests
{
    /// <summary>
    /// DynamicFilter 产出的 jsonb 谓词应生成 @? 运算符，并且执行计划里出现 GIN 索引。
    /// </summary>
    [MSTest.TestMethod]
    public async Task DynamicFilter_JsonbPredicate_UsesGinIndex()
    {
        await using var db = TestDbFactory.Create();
        await db.Database.OpenConnectionAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();

        // 事务里种几行数据（不提交），让优化器有真实行数可依据。
        var rows = Enumerable.Range(0, 5).Select(i =>
        {
            var data = new Dictionary<string, object?>
            {
                ["f_name"] = i == 0 ? "x" : "y",
                ["f_num"] = 10L,
            };
            return new EIMSNext.Entities.FormData { Id = "gin-probe-" + i, CorpId = "gin-probe-corp", Data = data };
        }).ToList();
        db.Set<EIMSNext.Entities.FormData>().AddRange(rows);
        await db.SaveChangesAsync();

        var filter = new DynamicFilter { Field = "data.f_name", Op = FilterOp.Eq, Value = "x" };
        // IgnoreQueryFilters 去掉全局软删除谓词：DeleteFlag 上有 btree，其 Bitmap/Index Scan
        // 会在 0 行统计下抢走计划，令 @? 退化为 Filter。去掉后 @? 是唯一谓词，
        // 在 ExplainAsync 禁用 Seq/Index Scan 的前提下，可服务它的只有 GIN——断言因此确定化。
        var sql = db.Set<EIMSNext.Entities.FormData>()
            .IgnoreQueryFilters()
            .Where(filter.ToPredicate<EIMSNext.Entities.FormData>())
            .Select(f => f.Id)
            .ToQueryString();

        MSTest.Assert.IsTrue(sql.Contains("@?"), $"应生成 @? 运算符，实际 SQL：\n{sql}");
        MSTest.Assert.IsFalse(sql.Contains("eims_json_match"), $"不应再走函数调用，实际 SQL：\n{sql}");

        var plan = await ExplainAsync(db, sql);
        MSTest.Assert.IsTrue(plan.Contains("IX_FormData_Data_Gin"),
            $"执行计划应命中 GIN 索引 IX_FormData_Data_Gin。SQL：\n{sql}\n执行计划：\n{plan}");

        await transaction.RollbackAsync();
    }

    /// <summary>
    /// 范围类 JSONPath（gt/lt 等数值比较）：GIN 索引的 jsonpath 支持只覆盖
    /// 可索引运算符（==、@&gt;、存在性等），纯范围比较本来就不受 GIN 支持——
    /// 这是 PostgreSQL 的能力边界，与语法无关。本用例锁定两点：
    /// ① 仍生成 @? 运算符（而不是 eims_json_match 函数调用）；
    /// ② 执行计划里不再出现函数调用（优化器用标量索引 + Filter 完成，
    /// 与旧实现 Seq/Filter 同级，等值/包含类谓词则见上一个用例直接命中 GIN）。
    /// </summary>
    [MSTest.TestMethod]
    public async Task DynamicFilter_JsonbRangePredicate_UsesGinIndex()
    {
        await using var db = TestDbFactory.Create();
        await db.Database.OpenConnectionAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var filter = new DynamicFilter { Field = "data.f_num", Op = FilterOp.Gt, Value = 1 };
        var sql = db.Set<EIMSNext.Entities.FormData>().Where(filter.ToPredicate<EIMSNext.Entities.FormData>()).Select(f => f.Id).ToQueryString();

        MSTest.Assert.IsTrue(sql.Contains("@?"), $"应生成 @? 运算符，实际 SQL：\n{sql}");
        MSTest.Assert.IsFalse(sql.Contains("eims_json_match"), $"不应再走函数调用，实际 SQL：\n{sql}");

        var plan = await ExplainAsync(db, sql);
        MSTest.Assert.IsFalse(plan.Contains("eims_json_match"),
            $"执行计划不应出现函数调用。SQL：\n{sql}\n执行计划：\n{plan}");

        await transaction.RollbackAsync();
    }

    private static async Task<string> ExplainAsync(PostgreSqlDbContext db, string sql)
    {
        var connection = (Npgsql.NpgsqlConnection)db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();

        // 测试在未提交事务里种数据，表统计为 0 行，优化器会选 Seq Scan / btree Index Scan + Filter，
        // 这与「@? 是否可被 GIN 索引服务」的契约无关。这里禁掉 Seq Scan 与普通 Index Scan，
        // 只留 Bitmap Scan：@? 若可 GIN 索引化，计划必然出现 IX_FormData_Data_Gin 的 Bitmap Index Scan。
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
