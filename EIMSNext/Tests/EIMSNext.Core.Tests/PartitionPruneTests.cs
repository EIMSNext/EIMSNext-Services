using System.Text;
using System.Text.RegularExpressions;

using EIMSNext.Persistence.PostgreSql;

using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EIMSNext.Core.Tests
{
    /// <summary>
    /// 守门用例：分区表的查询必须真的发生分区剪枝，而不是「看起来分了区、实际全分区扫」。
    /// </summary>
    /// <remarks>
    /// <para>
    /// PG 哈希分区只有当查询对分区键的<b>全部列</b>都有等值条件时才能剪枝
    /// （多列哈希缺任一列 = 全分区扫）。历史教训是查询路径少带一个条件，分区就静默失效；
    /// 这里用 EXPLAIN 把「剪枝是否发生」变成断言。
    /// </para>
    /// <para>
    /// 断言口径：解析 EXPLAIN 文本里出现的子分区名。带全分区键的查询必须只落在 1 个分区上；
    /// 只带一级键的查询（by-key / 应用级聚合）必须落在同一个一级分区的叶子集合里。
    /// </para>
    /// </remarks>
    [TestClass]
    public sealed class PartitionPruneTests
    {
        private const string SmokeCorp = "__prune_probe_corp";
        private const string SmokeForm = "__prune_probe_form";

        [TestMethod]
        public void FormData_WithCorpAndForm_PrunesToSingleLeaf()
        {
            var plan = Explain($"""
                select * from "FormData"
                where "CorpId" = '{SmokeCorp}' and "FormId" = '{SmokeForm}'
                """);

            var leaves = Collect(plan, "FormData_p\\d\\d_h\\d\\d");
            Assert.AreEqual(1, leaves.Count, $"FormData 带 CorpId+FormId 应剪枝到单个叶分区。计划：{plan}");
        }

        [TestMethod]
        public void FormData_WithCorpOnly_PrunesToOneLevel1Partition()
        {
            // by-key 与应用级聚合只带 CorpId：第一层剪枝后仍需扫该分区的 8 个叶子，
            // 断言落在同一个一级分区（而不是 16 个一级分区全扫）。
            var plan = Explain($"""
                select * from "FormData" where "CorpId" = '{SmokeCorp}'
                """);

            var level1 = Collect(plan, "FormData_p\\d\\d(?=_h\\d\\d)");
            Assert.AreEqual(1, level1.Count, $"FormData 只带 CorpId 应剪枝到单个一级分区。计划：{plan}");
        }

        [TestMethod]
        public void FormDataChangeLog_WithCorp_PrunesToSinglePartition()
        {
            var plan = Explain($"""
                select * from "FormDataChangeLog" where "CorpId" = '{SmokeCorp}'
                """);

            var partitions = Collect(plan, "FormDataChangeLog_p\\d\\d");
            Assert.AreEqual(1, partitions.Count, $"FormDataChangeLog 只带 CorpId 应剪枝到单分区。计划：{plan}");
        }

        [TestMethod]
        public void WfTaskLog_WithCorp_PrunesToSinglePartition()
        {
            var plan = Explain($"""
                select * from "Wf_TaskLog" where "CorpId" = '{SmokeCorp}'
                """);

            var partitions = Collect(plan, "Wf_TaskLog_p\\d\\d");
            Assert.AreEqual(1, partitions.Count, $"Wf_TaskLog 只带 CorpId 应剪枝到单分区。计划：{plan}");
        }

        [TestMethod]
        public void AuditLog_WithinOneMonth_PrunesToSinglePartition()
        {
            // 2026-06 的毫秒区间，落在锚点窗口（2026-01 起 24 个月）内。
            var plan = Explain("""
                select * from "AuditLog"
                where "CreateTime" >= 1780272000000 and "CreateTime" < 1782864000000
                """);

            var partitions = Collect(plan, "AuditLog_\\d{4}_\\d\\d");
            Assert.AreEqual(1, partitions.Count, $"AuditLog 按单月范围应剪枝到单分区。计划：{plan}");
        }

        [TestMethod]
        public void PartitionedTables_HaveNoPrimaryKeyViolationOnInsertRouting()
        {
            // 冒烟：三张哈希分区表各插一行（含二级键），验证路由与复合主键的写入路径，
            // 事务内回滚不留数据。
            using var db = TestDbFactory.Create();
            using var transaction = db.Database.BeginTransaction();
            try
            {
                db.Database.ExecuteSqlRaw($"""
                    insert into "FormData" ("Id", "CorpId", "FormId", "AppId", "CreateTime", "DeleteFlag", "Data")
                    values ('{SmokeCorp}_fd', '{SmokeCorp}', '{SmokeForm}', '__prune_app', 0, false, 'null'::jsonb);
                    insert into "FormDataChangeLog" ("Id", "CorpId", "FormId", "DataId", "Content", "CreateTime", "DeleteFlag")
                    values ('{SmokeCorp}_cl', '{SmokeCorp}', '{SmokeForm}', '{SmokeCorp}_data', '[]'::jsonb, 0, false);
                    insert into "Wf_TaskLog" ("Id", "CorpId", "FormId", "DataId", "DataBrief", "CreateTime", "DeleteFlag")
                    values ('{SmokeCorp}_tl', '{SmokeCorp}', '{SmokeForm}', '{SmokeCorp}_data', '[]'::jsonb, 0, false);
                    """);
            }
            finally
            {
                transaction.Rollback();
            }
        }

        [TestMethod]
        public void RangePartition_PreCreateDdl_IsIdempotentAndRoutesFutureRows()
        {
            // 预建作业（PartitionMaintainJob）用同一份 DDL 生成逻辑：2028-01 在锚点窗口之外
            // （default 兜底区），预建后该月行必须路由进新分区而不是 default。
            using var db = TestDbFactory.Create();
            using var transaction = db.Database.BeginTransaction();
            try
            {
                var ddl = PostgreSqlBaselineScript.RenderRangePartitionDdl("AuditLog", 2028, 1);
                db.Database.ExecuteSqlRaw(ddl);
                db.Database.ExecuteSqlRaw(ddl);    // 幂等：第二次执行应静默跳过

                db.Database.ExecuteSqlRaw($"""
                    insert into "AuditLog" ("Id", "CorpId", "CreateTime")
                    values ('{SmokeCorp}_future', '{SmokeCorp}', 1830400000000);
                    """);

                var partition = Scalar(db, """
                    select c.relname from pg_class c
                    where c.oid = (select tableoid from "AuditLog" where "Id" = '__prune_probe_corp_future')
                    """);
                Assert.AreEqual("AuditLog_2028_01", partition, $"预建分区应接收 2028-01 的行。DDL：{ddl}");
            }
            finally
            {
                transaction.Rollback();
            }
        }

        /// <summary>执行单值查询。</summary>
        private static string Scalar(PostgreSqlDbContext db, string sql)
        {
            using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            return (string)command.ExecuteScalar()!;
        }

        /// <summary>执行 EXPLAIN（纯文本），返回计划全文。</summary>
        private static string Explain(string sql)
        {
            using var db = TestDbFactory.Create();
            db.Database.OpenConnection();
            try
            {
                using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = "explain " + sql;
                using var reader = command.ExecuteReader();

                var plan = new StringBuilder();
                while (reader.Read()) plan.AppendLine(reader.GetString(0));
                return plan.ToString();
            }
            finally
            {
                db.Database.CloseConnection();
            }
        }

        /// <summary>收集计划文本里出现的去重分区名。</summary>
        private static List<string> Collect(string plan, string pattern)
            => Regex.Matches(plan, pattern)
                .Select(x => x.Value)
                .Distinct(StringComparer.Ordinal)
                .ToList();
    }
}
