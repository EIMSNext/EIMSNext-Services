using System.Text;
using Microsoft.EntityFrameworkCore;

namespace EIMSNext.Persistence.PostgreSql;

/// <summary>
/// 把 <see cref="PostgreSqlDbContext"/> 的 EF 模型投影成 DbMaintenance 迁移链的基线脚本。
/// </summary>
/// <remarks>
/// <para>
/// 迁移脚本 <c>001_CreateTables.sql</c> 与 <c>002_CreateIndexes.sql</c> 的「模型段」不是手写的，
/// 而是 <c>DbContext.Database.GenerateCreateScript()</c> 的确定性投影，再叠加三条规范化规则：
/// </para>
/// <list type="number">
/// <item><description>表按表名（<see cref="StringComparer.Ordinal"/>）排序，索引按「表名 → 索引名」排序；</description></item>
/// <item><description>列修饰符统一小写（<c>not null</c>），与索引段的手写风格保持一致；</description></item>
/// <item><description>给 NOT NULL 的标量列补中立默认值，便于手工与原生 SQL 插入；
/// <c>jsonb</c>、数组、时间戳等刻意不补（补默认值会改变语义或直接非法）。</description></item>
/// </list>
/// <para>
/// 生成入口与守门用例都在 <c>Tests/EIMSNext.Core.Tests/BaselineScriptTests</c>：
/// 前者写盘，后者断言磁盘内容与模型投影逐字节一致，防止「改了模型忘了刷脚本」。
/// </para>
/// <para>
/// 投影只读模型元数据，不会真正连库；<see cref="DesignTimeConnectionString"/> 仅是
/// <c>UseNpgsql</c> 的占位值。
/// </para>
/// </remarks>
public static class PostgreSqlBaselineScript
{
    /// <summary>实体表基线脚本文件名。</summary>
    public const string CreateTablesFileName = "001_CreateTables.sql";

    /// <summary>索引基线脚本文件名。</summary>
    public const string CreateIndexesFileName = "002_CreateIndexes.sql";

    /// <summary>「模型声明索引」段的起始标记；标记之间的内容由本类重新生成。</summary>
    public const string ModelIndexesBeginMarker = "-- >>> generated: model-declared indexes";

    /// <summary>「模型声明索引」段的结束标记。</summary>
    public const string ModelIndexesEndMarker = "-- <<< generated: model-declared indexes";

    /// <summary>生成注释里出现的重新生成命令，供脚本头部引用。</summary>
    private const string RegenerateCommand =
        "dotnet test Tests/EIMSNext.Core.Tests --filter RegenerateBaselineScripts（需先置环境变量 EIMS_REGENERATE_BASELINE=1）";

    /// <summary>投影用的占位连接串：只读模型元数据，不会连库。</summary>
    private const string DesignTimeConnectionString =
        "Host=localhost;Port=5432;Database=eimsnext_baseline_projection;Username=postgres;Password=postgres";

    /// <summary>
    /// 补中立默认值的最小列类型集合。
    /// 只覆盖「NOT NULL 且补默认值不改变语义」的标量类型：
    /// <c>uuid</c>（空串非法）、时间戳（默认值应体现业务时间而非纪元）、<c>jsonb</c> 与数组（空值语义各家不同）都不在其中。
    /// </summary>
    private static readonly Dictionary<string, string> NeutralDefaults = new(StringComparer.Ordinal)
    {
        ["text"] = "''",
        ["character varying"] = "''",
        ["integer"] = "0",
        ["bigint"] = "0",
        ["smallint"] = "0",
        ["numeric"] = "0",
        ["real"] = "0",
        ["double precision"] = "0",
        ["boolean"] = "false",
    };

    /// <summary>
    /// 渲染 <c>001_CreateTables.sql</c> 的完整内容（含文件头注释）。
    /// </summary>
    public static string RenderCreateTables()
    {
        var tables = ReadCreateTables();

        var lines = new List<string>();
        AppendBlock(lines, CreateTablesHeader);

        foreach (var (_, block) in tables)
        {
            // 文件头与各表块之间、表块与表块之间一律空两行，与原有脚本的排版一致。
            lines.Add(string.Empty);
            lines.Add(string.Empty);
            lines.AddRange(block);
        }

        return ToPlatformNewLines(string.Join('\n', lines) + "\n");
    }

    /// <summary>
    /// 渲染「模型声明索引」段的全部行（含首尾标记），供
    /// <see cref="ApplyModelIndexes"/> 替换 <c>002_CreateIndexes.sql</c> 中的同名段落。
    /// </summary>
    public static IReadOnlyList<string> RenderModelIndexes()
    {
        var indexes = ReadModelIndexes();

        var lines = new List<string>
        {
            ModelIndexesBeginMarker,
            "-- 本段由 EF 模型投影生成，请勿手工编辑；新增索引优先写在模型里，模型无法表达的（GIN、" +
            "部分索引、text_pattern_ops）写在本标记段之下的手写区。",
            $"-- 重新生成：{RegenerateCommand}",
        };

        for (var index = 0; index < indexes.Count; index++)
        {
            lines.Add(indexes[index].Statement);
            if (index < indexes.Count - 1)
                lines.Add(string.Empty);
        }

        lines.Add(ModelIndexesEndMarker);
        return lines;
    }

    /// <summary>
    /// 把 <paramref name="existingContent"/> 里标记段（含标记行）替换成重新生成的模型索引段。
    /// </summary>
    /// <exception cref="InvalidOperationException">文件里找不到成对的标记行。</exception>
    public static string ApplyModelIndexes(string existingContent)
    {
        var newLine = DetectNewLine(existingContent);
        var lines = SplitLines(existingContent);

        var begin = lines.FindIndex(line => line.TrimEnd() == ModelIndexesBeginMarker);
        var end = lines.FindIndex(line => line.TrimEnd() == ModelIndexesEndMarker);

        if (begin < 0 || end < 0 || end < begin)
        {
            throw new InvalidOperationException(
                $"{CreateIndexesFileName} 缺少成对的模型索引段标记。" +
                $"请手工插入 {ModelIndexesBeginMarker} / {ModelIndexesEndMarker} 后重试。");
        }

        var rebuilt = new List<string>(lines.Count);
        rebuilt.AddRange(lines.Take(begin));
        rebuilt.AddRange(RenderModelIndexes());
        rebuilt.AddRange(lines.Skip(end + 1));

        // SplitLines 会丢掉末尾空行；脚本一律以换行结尾，这里固定补回（否则会写出 "No newline at end of file"）。
        return string.Join(newLine, rebuilt) + newLine;
    }

    /// <summary>读取脚本时统一按行切分（去掉行尾 CR，忽略末尾空行）。</summary>
    private static List<string> SplitLines(string content)
    {
        var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
        while (lines.Count > 0 && lines[^1].Length == 0)
            lines.RemoveAt(lines.Count - 1);
        return lines;
    }

    /// <summary>沿用原文件的换行符风格，避免只改一段内容却整文件重写行尾。</summary>
    private static string DetectNewLine(string content)
        => content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    /// <summary>把以 <c>\n</c> 组织的文本转成当前平台的换行符。</summary>
    private static string ToPlatformNewLines(string text)
        => text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\n", Environment.NewLine, StringComparison.Ordinal);

    /// <summary>把多行文本按行追加到目标集合。</summary>
    private static void AppendBlock(List<string> lines, string text)
        => lines.AddRange(SplitLines(text));

    /// <summary>调用 EF 生成完整建表脚本（含模型声明的索引）。</summary>
    private static string GenerateScript()
    {
        var options = new DbContextOptionsBuilder<PostgreSqlDbContext>()
            .UseNpgsql(DesignTimeConnectionString)
            .Options;

        using var context = new PostgreSqlDbContext(options);
        return context.Database.GenerateCreateScript();
    }

    /// <summary>按表名排序后的建表语句块（每块含首行 <c>CREATE TABLE</c> 与末行 <c>);</c>）。</summary>
    private static List<(string Table, List<string> Block)> ReadCreateTables()
    {
        const string createTablePrefix = "CREATE TABLE ";

        var lines = SplitLines(GenerateScript());
        var tables = new List<(string, List<string>)>();

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index].TrimEnd();
            if (!line.StartsWith(createTablePrefix, StringComparison.Ordinal)) continue;

            var block = new List<string> { line };
            var terminated = line.EndsWith(");", StringComparison.Ordinal);
            while (!terminated && index + 1 < lines.Count)
            {
                index++;
                var inner = lines[index].TrimEnd();
                block.Add(inner);
                terminated = inner.EndsWith(");", StringComparison.Ordinal);
            }

            if (!terminated)
                throw new InvalidOperationException($"建表语句未正常结束：{line}");

            tables.Add((ReadTableName(line), NormalizeTableBlock(block)));
        }

        if (tables.Count == 0)
            throw new InvalidOperationException("EF 模型没有产出任何建表语句。");

        return tables.OrderBy(x => x.Item1, StringComparer.Ordinal).ToList();
    }

    /// <summary>模型声明的索引，按「表名 → 索引名」排序。</summary>
    private static List<(string Table, string Name, string Statement)> ReadModelIndexes()
    {
        const string createIndexPrefix = "CREATE INDEX ";
        const string createUniqueIndexPrefix = "CREATE UNIQUE INDEX ";

        var lines = SplitLines(GenerateScript());
        var indexes = new List<(string, string, string)>();

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (!line.StartsWith(createIndexPrefix, StringComparison.Ordinal)
                && !line.StartsWith(createUniqueIndexPrefix, StringComparison.Ordinal))
                continue;

            indexes.Add(NormalizeIndexStatement(line));
        }

        return indexes
            .OrderBy(x => x.Item1, StringComparer.Ordinal)
            .ThenBy(x => x.Item2, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>从 <c>CREATE TABLE "X" (</c> 提取表名。</summary>
    private static string ReadTableName(string line)
    {
        var firstQuote = line.IndexOf('"');
        var secondQuote = firstQuote < 0 ? -1 : line.IndexOf('"', firstQuote + 1);
        if (secondQuote < 0)
            throw new InvalidOperationException($"无法从建表语句中解析表名：{line}");

        return line[(firstQuote + 1)..secondQuote];
    }

    /// <summary>规范化单个表块：列修饰符小写 + 补中立默认值；主键约束行原样保留。</summary>
    private static List<string> NormalizeTableBlock(List<string> block)
    {
        var result = new List<string>(block.Count);
        for (var index = 0; index < block.Count; index++)
        {
            var line = block[index];
            var isFirst = index == 0;
            var isConstraint = line.TrimStart().StartsWith("CONSTRAINT ", StringComparison.Ordinal);

            result.Add(isFirst || isConstraint ? line : NormalizeColumnLine(line));
        }

        return result;
    }

    /// <summary>
    /// 规范化列定义行：<c>"Name" text NOT NULL,</c> → <c>"Name" text not null default '',</c>。
    /// 非 NOT NULL 列只做修饰符小写，不加默认值。
    /// </summary>
    private static string NormalizeColumnLine(string line)
    {
        const string notNullSuffix = " NOT NULL";

        var trimmed = line.TrimEnd();
        var hasComma = trimmed.EndsWith(',');
        var body = hasComma ? trimmed[..^1] : trimmed;

        if (!body.EndsWith(notNullSuffix, StringComparison.Ordinal)) return line;

        // 标识符列带了 COLLATE "C" 之后，列定义形如 `"Id" text COLLATE "C"`：
        // 直接把 COLLATE 子句当成类型的一部分会匹配不上 NeutralDefaults（那里只有裸的 text），
        // NOT NULL 列就会静默丢掉 default ''。因此先把排序规则子句摘出来，补完默认值再拼回去。
        var withoutCollation = ExtractCollation(body[..^notNullSuffix.Length], out var collation);
        var type = ReadColumnType(withoutCollation);

        var defaultValue = NeutralDefaults.TryGetValue(type, out var value) ? $" default {value}" : string.Empty;
        var restored = collation.Length == 0 ? withoutCollation : withoutCollation + " " + collation;
        var result = restored + " not null" + defaultValue;

        return hasComma ? result + "," : result;
    }

    /// <summary>列定义行里 <c>COLLATE</c> 子句的起始标记（EF 生成时大写，这里按大小写无关匹配）。</summary>
    private const string CollationClause = "COLLATE";

    /// <summary>
    /// 把列定义中的 <c>COLLATE "C"</c> 子句摘出来。
    /// </summary>
    /// <param name="declaration">去掉 <c>NOT NULL</c> 之后的列定义，形如 <c>"Id" text COLLATE "C"</c>。</param>
    /// <param name="collation">摘出的排序规则子句；无 <c>COLLATE</c> 时为空串。</param>
    /// <returns>去掉排序规则子句后的列定义。</returns>
    private static string ExtractCollation(string declaration, out string collation)
    {
        var index = declaration.IndexOf(CollationClause, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            collation = string.Empty;
            return declaration;
        }

        // 子句一定延伸到行尾（列定义里 COLLATE 之后不会再有其它修饰符了），直接取剩余部分。
        collation = declaration[index..].Trim();
        return declaration[..index].TrimEnd();
    }

    /// <summary>从 <c>    "Name" text</c> 提取列类型（引号之后、去空白、保留原大小写）。</summary>
    private static string ReadColumnType(string declaration)
    {
        var firstQuote = declaration.IndexOf('"');
        var secondQuote = firstQuote < 0 ? -1 : declaration.IndexOf('"', firstQuote + 1);
        if (secondQuote < 0)
            throw new InvalidOperationException($"无法从列定义中解析列名：{declaration}");

        return declaration[(secondQuote + 1)..].Trim();
    }

    /// <summary>
    /// 把 EF 的索引语句改写成与手写区一致的幂等风格：
    /// <c>CREATE UNIQUE INDEX "X" ON "T" (...)</c> → <c>create unique index if not exists "X" on "T" (...)</c>。
    /// </summary>
    private static (string Table, string Name, string Statement) NormalizeIndexStatement(string statement)
    {
        const string createIndexPrefix = "CREATE INDEX ";
        const string createUniqueIndexPrefix = "CREATE UNIQUE INDEX ";
        const string onSeparator = " ON ";

        var body = statement.TrimEnd().TrimEnd(';');

        string rebuilt;
        if (body.StartsWith(createUniqueIndexPrefix, StringComparison.Ordinal))
            rebuilt = "create unique index if not exists " + body[createUniqueIndexPrefix.Length..];
        else if (body.StartsWith(createIndexPrefix, StringComparison.Ordinal))
            rebuilt = "create index if not exists " + body[createIndexPrefix.Length..];
        else
            throw new InvalidOperationException($"无法识别的索引语句：{statement}");

        // 只替换紧跟索引名的那一个 ON，避免误伤索引表达式里的同名片段。
        var onIndex = rebuilt.IndexOf(onSeparator, StringComparison.Ordinal);
        if (onIndex >= 0)
            rebuilt = rebuilt[..onIndex] + " on " + rebuilt[(onIndex + onSeparator.Length)..];

        var nameStart = rebuilt.IndexOf('"') + 1;
        var nameEnd = rebuilt.IndexOf('"', nameStart);
        var name = rebuilt[nameStart..nameEnd];

        var tableStart = rebuilt.IndexOf(" on \"", StringComparison.Ordinal) + 5;
        var tableEnd = rebuilt.IndexOf('"', tableStart);
        var table = rebuilt[tableStart..tableEnd];

        return (table, name, rebuilt + ";");
    }

    /// <summary><c>001_CreateTables.sql</c> 的文件头注释。</summary>
    private const string CreateTablesHeader = """
        -- EIMSNext PostgreSQL 基线结构 —— 实体表。
        --
        -- 【唯一依据】本文件是 EF 模型的投影，模型才是表结构的真源：
        --     Core/EIMSNext.Persistence.PostgreSql/PostgreSqlDbContext.cs
        -- 生成方式：PostgreSqlBaselineScript.RenderCreateTables()，输入为
        --     dbContext.Database.GenerateCreateScript()，之后做三件事：
        --   1) 表按表名（Ordinal）排序；
        --   2) 列修饰符统一小写（not null）；
        --   3) 给 NOT NULL 的标量列补中立默认值（text '' / integer、bigint、numeric 0 / boolean false），
        --      便于手工和原生 SQL 插入；EF 写入时始终显式赋值，不依赖这些默认值。
        --      刻意不补的类型：jsonb、数组、uuid、时间戳 —— 补默认值会改变语义或直接非法。
        --
        -- 重新生成（会同时刷新 002 的模型声明索引段）：
        --     置环境变量 EIMS_REGENERATE_BASELINE=1 后执行
        --     dotnet test Tests/EIMSNext.Core.Tests --filter RegenerateBaselineScripts
        -- 一致性由 Tests/EIMSNext.Core.Tests/BaselineScriptTests 与 SchemaConsistencyTests 守住。
        --
        -- 因此要改表结构时：先改实体与 DbContext，再重新生成本文件，
        -- 不要在迁移链尾部追加「改列」脚本（全新起步，不承担向前兼容）。
        --
        -- 索引见 002_CreateIndexes.sql；WorkflowCore 与 Quartz 的第三方存储表见 003~006。
        -- 表名规则：精确实体名单数 + 引号标识符（Wf_* / Ef_* 沿用下划线前缀的既有约定）。
        -- 时间戳列 CreateTime / UpdateTime 为 Unix 毫秒，故用 bigint；审计列 CreateBy / UpdateBy 为 jsonb。
        """;
}
