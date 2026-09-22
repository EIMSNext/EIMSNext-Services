using EIMSNext.Persistence.PostgreSql;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EIMSNext.Core.Tests
{
    /// <summary>
    /// 守门用例：DbMaintenance 迁移链里的基线脚本必须与 EF 模型投影逐字节一致。
    /// <para>
    /// 背景：<c>001_CreateTables.sql</c> / <c>002_CreateIndexes.sql</c> 是模型的投影，
    /// 但投影本身没有编译期约束——改了实体却忘了刷脚本，只会在连库写入时才炸
    /// （历史事故：<c>CreateBy</c>/<c>UpdateBy</c> 两列模型里有、建表脚本里没有，
    /// 任何 INSERT 都报 <c>42703 column does not exist</c>）。
    /// 这里把「磁盘脚本 == 模型投影」变成一条断言，漂移在测试阶段就暴露。
    /// </para>
    /// <para>
    /// 投影规则见 <see cref="PostgreSqlBaselineScript"/>；
    /// 与真实库结构的比对由 <see cref="SchemaConsistencyTests"/> 负责。
    /// </para>
    /// </summary>
    [TestClass]
    public sealed class BaselineScriptTests
    {
        /// <summary>
        /// 生成入口：按当前 EF 模型重新生成两个基线脚本并写盘。
        /// 改完实体或 <see cref="PostgreSqlDbContext"/> 后执行它，而不是手抄列定义。
        /// </summary>
        /// <remarks>
        /// 默认<b>不执行</b>（<see cref="Assert.Inconclusive(string)"/>，在测试报告里记为跳过），
        /// 只有置了环境变量 <c>EIMS_REGENERATE_BASELINE=1</c> 才会写盘：
        /// <code>
        /// $env:EIMS_REGENERATE_BASELINE = 1
        /// dotnet test Tests/EIMSNext.Core.Tests --filter RegenerateBaselineScripts
        /// </code>
        /// 之所以不用 <see cref="IgnoreAttribute"/>：被 Ignore 的用例无法被 <c>--filter</c> 选中执行，
        /// 而写盘必须是一条可脚本化的命令。
        /// </remarks>
        [TestMethod]
        public void RegenerateBaselineScripts()
        {
            if (!string.Equals(
                    Environment.GetEnvironmentVariable("EIMS_REGENERATE_BASELINE"),
                    "1",
                    StringComparison.Ordinal))
            {
                Assert.Inconclusive(
                    "生成入口默认不执行；置 EIMS_REGENERATE_BASELINE=1 后重跑本用例才会写盘。");
                return;
            }

            var sqlDirectory = ResolveSqlDirectory();

            var createTablesPath = Path.Combine(sqlDirectory, PostgreSqlBaselineScript.CreateTablesFileName);
            File.WriteAllText(createTablesPath, PostgreSqlBaselineScript.RenderCreateTables());

            var createIndexesPath = Path.Combine(sqlDirectory, PostgreSqlBaselineScript.CreateIndexesFileName);
            File.WriteAllText(
                createIndexesPath,
                PostgreSqlBaselineScript.ApplyModelIndexes(File.ReadAllText(createIndexesPath)));
        }

        /// <summary><c>001_CreateTables.sql</c> 必须等于模型投影。</summary>
        [TestMethod]
        public void CreateTablesScript_MatchesModelProjection()
        {
            var path = Path.Combine(ResolveSqlDirectory(), PostgreSqlBaselineScript.CreateTablesFileName);

            Assert.AreEqual(
                Normalize(PostgreSqlBaselineScript.RenderCreateTables()),
                Normalize(File.ReadAllText(path)),
                $"{PostgreSqlBaselineScript.CreateTablesFileName} 与 EF 模型投影不一致。" +
                Environment.NewLine +
                "修正方式：执行 BaselineScriptTests.RegenerateBaselineScripts 重新生成，" +
                "不要手改脚本内容。");
        }

        /// <summary><c>002_CreateIndexes.sql</c> 的模型声明索引段必须等于模型投影。</summary>
        [TestMethod]
        public void CreateIndexesScriptModelSection_MatchesModelProjection()
        {
            var path = Path.Combine(ResolveSqlDirectory(), PostgreSqlBaselineScript.CreateIndexesFileName);
            var onDisk = File.ReadAllText(path);

            Assert.AreEqual(
                Normalize(onDisk),
                Normalize(PostgreSqlBaselineScript.ApplyModelIndexes(onDisk)),
                $"{PostgreSqlBaselineScript.CreateIndexesFileName} 的模型声明索引段与 EF 模型投影不一致。" +
                Environment.NewLine +
                "修正方式：执行 BaselineScriptTests.RegenerateBaselineScripts 重新生成，" +
                "手写区（标记段之下）不受影响。");
        }

        /// <summary>比对前统一换行符，避免 CRLF/LF 差异造成假失败。</summary>
        private static string Normalize(string content)
            => content.Replace("\r\n", "\n", StringComparison.Ordinal);

        /// <summary>定位迁移脚本目录（仓库内源码目录，不是 bin 下的拷贝）。</summary>
        private static string ResolveSqlDirectory()
        {
            DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EIMSNext.sln")))
                directory = directory.Parent;

            if (directory is null)
                throw new DirectoryNotFoundException("EIMSNext.sln was not found.");

            return Path.Combine(directory.FullName, "ApiHost", "EIMSNext.Tool.DbMaintenance", "Sql");
        }
    }
}
