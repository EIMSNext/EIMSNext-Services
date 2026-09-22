using EIMSNext.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EIMSNext.Core.Tests
{
    /// <summary>
    /// 守门用例：<see cref="PostgreSqlDbContext"/> 的 EF 模型必须与实际库结构一致。
    /// <para>
    /// 背景：迁移期间出现过两类问题，都是靠这个比对发现的——
    /// 1) <c>EntityBase.CreateBy</c>/<c>UpdateBy</c> 被模型映射，但建表脚本从未创建这两列，
    ///    导致任何 INSERT 都报 <c>42703 column does not exist</c>；
    /// 2) 若干部件被登记进模型后，其内嵌集合缺 jsonb 值转换器，模型校验阶段直接抛异常。
    /// 这里把「模型 → 列清单（名称 + 类型）」与 <c>information_schema</c> 逐表比对，
    /// 让这类漂移在测试阶段就暴露，而不是等到线上写入失败。
    /// </para>
    /// </summary>
    [TestClass]
    public sealed class SchemaConsistencyTests
    {
        [TestMethod]
        public void ModelTablesAndColumnsMatchDatabase()
        {
            using var db = TestDbFactory.Create();

            var expected = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.Ordinal);
            foreach (var entityType in db.Model.GetEntityTypes())
            {
                var table = entityType.GetTableName();
                if (table is null) continue;

                var columns = new SortedDictionary<string, string>(StringComparer.Ordinal);
                foreach (var property in entityType.GetProperties())
                {
                    columns[property.GetColumnName()] = NormalizeEfType(property.GetColumnType());
                }
                expected[table] = columns;
            }

            Assert.IsTrue(expected.Count > 0, "模型里没有任何映射到表的实体。");

            var actual = ReadDatabaseColumns(db);

            var problems = new List<string>();
            foreach (var (table, columns) in expected)
            {
                if (!actual.TryGetValue(table, out var dbColumns))
                {
                    problems.Add($"表缺失：\"{table}\" 在模型中存在，但库中不存在。");
                    continue;
                }

                foreach (var (column, efType) in columns)
                {
                    if (!dbColumns.TryGetValue(column, out var dbType))
                    {
                        problems.Add($"列缺失：\"{table}\".\"{column}\"（模型期望 {efType}）。");
                    }
                    else if (!string.Equals(efType, dbType, StringComparison.Ordinal))
                    {
                        problems.Add($"列类型不符：\"{table}\".\"{column}\" 模型期望 {efType}，库中为 {dbType}。");
                    }
                }

                foreach (var column in dbColumns.Keys)
                {
                    if (!columns.ContainsKey(column))
                        problems.Add($"多余列：\"{table}\".\"{column}\" 库里存在，但模型未映射。");
                }
            }

            if (problems.Count == 0) return;

            Assert.Fail(
                $"EF 模型与数据库结构不一致，共 {problems.Count} 处。" +
                Environment.NewLine +
                "修正方式：改实体/DbContext 后按 BaselineScriptTests 的说明重新生成 " +
                "ApiHost/EIMSNext.Tool.DbMaintenance/Sql/001_CreateTables.sql，" +
                "再重建测试库跑迁移链；不要手改已执行的迁移脚本。" +
                Environment.NewLine +
                string.Join(Environment.NewLine, problems.Take(60)));
        }

        /// <summary>读取 public schema 的真实列结构。</summary>
        private static Dictionary<string, SortedDictionary<string, string>> ReadDatabaseColumns(DbContext db)
        {
            var result = new Dictionary<string, SortedDictionary<string, string>>(StringComparer.Ordinal);

            db.Database.OpenConnection();
            try
            {
                using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = """
                    select table_name, column_name, data_type
                    from information_schema.columns
                    where table_schema = 'public'
                    """;
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var table = reader.GetString(0);
                    var column = reader.GetString(1);
                    var dataType = reader.GetString(2);

                    if (!result.TryGetValue(table, out var columns))
                    {
                        columns = new SortedDictionary<string, string>(StringComparer.Ordinal);
                        result[table] = columns;
                    }
                    columns[column] = NormalizeDbType(dataType);
                }
            }
            finally
            {
                db.Database.CloseConnection();
            }

            return result;
        }

        /// <summary>把 EF 侧的列类型折算成与 information_schema.data_type 可比的写法。</summary>
        private static string NormalizeEfType(string? columnType)
        {
            var type = (columnType ?? string.Empty).Trim().ToLowerInvariant();
            if (type.EndsWith("[]", StringComparison.Ordinal)) return "array";
            if (type.StartsWith("varchar", StringComparison.Ordinal)
                || type.StartsWith("character varying", StringComparison.Ordinal)) return "character varying";
            return type;
        }

        /// <summary>把 information_schema 的 data_type 折算到同一口径。</summary>
        private static string NormalizeDbType(string dataType)
            => dataType.Trim().ToLowerInvariant();
    }
}
