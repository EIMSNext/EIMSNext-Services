# EIMSNext.Tool.DbMaintenance

PostgreSQL Schema 迁移执行器。按版本号顺序、带 SHA256 台账地执行 `Sql/` 下的迁移脚本。

## 配置来源

优先级由低到高（`Host.CreateApplicationBuilder` 的默认链）：

1. 当前目录下的 `appsettings.json`
2. 当前目录下的 `appsettings.{Environment}.json`
3. 环境变量
4. 命令行参数

需要提供的配置：

- `PostgreSql:ConnectionString`
- `DbMaintenance:SqlDirectory`（默认 `Sql`）
- `DbMaintenance:ValidateChecksums`（默认 `true`）
- `DbMaintenance:AllowDestructiveChanges`（默认 `false`）

## 运行方式

```powershell
dotnet run --project EIMSNext-Services/EIMSNext/ApiHost/EIMSNext.Tool.DbMaintenance/EIMSNext.Tool.DbMaintenance.csproj
```

切换目标库（例如指向测试库 `EIMSTest`）：

```powershell
$env:PostgreSql__ConnectionString = "Host=localhost;Port=5432;Database=EIMSTest;Username=postgres;Password=sa123"
dotnet run --project EIMSNext-Services/EIMSNext/ApiHost/EIMSNext.Tool.DbMaintenance/EIMSNext.Tool.DbMaintenance.csproj
```

## 命令行开关

| 开关 | 作用 |
| --- | --- |
| `--dry-run` | 只列出将要执行的脚本，不落库 |
| `--verify` | 只校验、不执行：磁盘缺文件 / checksum 变化 / **存在未应用的 pending 脚本**都会报错 |
| `--target-version <版本>` | 执行到指定版本为止 |
| `--accept-checksum-change` | 允许已记录脚本内容发生变化（默认拒绝） |

## 脚本链（`Sql/`）

| 脚本 | 内容 | 来源 |
| --- | --- | --- |
| `000_CreateCaseInsensitiveType.sql` | 创建 `citext` 扩展（业务字符列用它实现大小写无关比较） | 本项目 |
| `001_CreateTables.sql` | 全部业务实体表（65 张） | `PostgreSqlBaselineScript.RenderCreateTables()` 自动生成 |
| `002_CreateIndexes.sql` | 模型声明索引段（自动）+ 手写业务索引（含 GIN） | 模型段自动生成，手写区人工维护 |
| `003_CreateWorkflowTables.sql` | WorkflowCore 存储表 | 第三方 |
| `004_CreateWorkflowIndexes.sql` | WorkflowCore 索引 | 第三方 |
| `005_CreateQuartzTables.sql` | Quartz 存储表 | 第三方 |
| `006_CreateQuartzIndexes.sql` | Quartz 索引 | 第三方 |
| `007_CreateJsonPathFunctions.sql` | `eims_json_text` / `eims_json_match` / `eims_json_sort` 三个 jsonb 路径辅助函数 | 本项目 |

**EF 模型是 PostgreSQL 表结构的唯一依据。** `001` 与 `002` 的模型段由模型投影而来，
投影入口已随仓库固化在 `Core/EIMSNext.Persistence.PostgreSql/PostgreSqlBaselineScript.cs`。

## 重新生成基线脚本

改完实体或 `PostgreSqlDbContext` 后，**不要手抄列定义**，跑生成入口：

```powershell
$env:EIMS_REGENERATE_BASELINE = 1
dotnet test Tests/EIMSNext.Core.Tests --filter RegenerateBaselineScripts
```

它会重写 `001_CreateTables.sql` 全文，并替换 `002_CreateIndexes.sql` 里
`-- >>> generated: model-declared indexes` 与 `-- <<< generated: model-declared indexes`
两行标记之间的内容 —— 标记之下的手写索引区不受影响。不置环境变量时该用例自跳过，
因此日常全量测试不会改动脚本。

投影规则（生成器内实现，注释里也有说明）：

- 表按表名（Ordinal）排序；索引按「表名 → 索引名」排序；
- 列修饰符统一小写 `not null`；
- 给 NOT NULL 的标量列补中立默认值（`text ''` / `integer`、`bigint`、`numeric 0` / `boolean false`），
  便于手工和原生 SQL 插入。`jsonb`、数组、`uuid`、时间戳刻意不补 —— 补默认值会改变语义或直接非法。

两个守门用例（全量测试时执行）：

- `BaselineScriptTests`：磁盘脚本必须逐字节等于模型投影，防止「改了模型忘了刷脚本」；
- `SchemaConsistencyTests`：模型必须与实际库的列名与列类型一致。

改表结构的正确姿势：**改实体与 `PostgreSqlDbContext` → 跑上面的生成命令 → 重建库跑迁移链**，
不要在迁移链尾部追加「改列」脚本。

`007` 是 jsonb 内部键查询的基础设施，**不可省略**：业务按 `data.<字段>` 过滤/排序
表单数据，而 EF Core 无法把 `ExpandoObject` 的索引器翻译成 SQL，必须落到这三个函数上。
三个函数按用途分工，**不能互相替代**：

| 函数 | 返回 | 用途 | 换成别的会怎样 |
| --- | --- | --- | --- |
| `eims_json_text` | `text` | 过滤时的取值比较 | — |
| `eims_json_match` | `boolean` | 过滤（带谓词的 JSONPath，`jsonb_path_exists`） | 用取值再比较会丢掉「数组任意元素满足」语义 |
| `eims_json_sort` | `jsonb` | **排序**（`ORDER BY`） | 用 `eims_json_text` 会退化成字典序，`"10"` 排在 `"9"` 前面 |

`eims_json_sort` 刻意**不声明 `strict`**，并在路径不可达时返回 jsonb 的 `'null'`
而不是 SQL NULL：jsonb 的 btree 顺序里 `Null` 最小，于是「字段缺失」自然落在升序最前 /
降序最后，正好是 Mongo 的 null 语义；返回 SQL NULL 反而会被 PostgreSQL 按
`NULLS LAST(ASC)` 处理，对不上。回归用例见 `DynamicRepositoryTest.SortByJsonbNumericFieldTest`。
详见脚本头部注释与 `EIMSNext.Core.Query.PgJsonFunctions`。

## 本轮重构期的特殊政策

本轮 Mongo→PostgreSQL 迁移是**从零起步**（本机不存在 `EIMS` 库，也不需要向前兼容）：

- 迁移链可以整体重建，`001` 的内容可以随模型变化而修改；
- 已有的临时库（如 `EIMSTest`）直接 **drop + create 后重跑迁移链**，
  不要用 `--accept-checksum-change` 去迁就旧库；
- 只跑测试库的完整链路验证：
  ```powershell
  $env:PostgreSql__ConnectionString = "Host=localhost;Port=5432;Database=EIMSTest;Username=postgres;Password=sa123"
  dotnet run --project ApiHost/EIMSNext.Tool.DbMaintenance/EIMSNext.Tool.DbMaintenance.csproj
  ```
  预期输出 `Applied 11 migration(s).`，全链一次通过。

## 硬性约定

- **已执行过的脚本一律不可改、不可删。** 台账里记了某版本而磁盘上没有同名文件会直接抛
  `Applied script is missing`；文件内容变了会抛 `Applied script checksum changed`。
  回退/修正只能**新增更高版本号的前向脚本**。
  （本轮重构期例外，见上节；进入正式环境后此条立即生效。）
- `001` 与 `002` 的模型段由生成器重写，**不要手工编辑**：手改会在下次生成时被覆盖，
  并被 `BaselineScriptTests` 判红。要调整表/索引结构请改模型。
- 脚本一律以换行结尾、编码 UTF-8。
- 脚本命名必须匹配 `^\d{3,}_[A-Za-z0-9_]+$`，版本前缀（`_` 前 3 位数字）全局唯一，
  且新脚本版本必须大于所有已应用版本。
- 含破坏性语句（`drop column` / `drop table` 等）的脚本需在文件首行写
  `-- destructive: true`，并把 `DbMaintenance:AllowDestructiveChanges` 置为 `true` 才会执行。
- 每个脚本包在**单个事务**里执行，任一语句失败即整体回滚。
- 执行期用 `pg_advisory_lock` 串行化，避免多实例并发迁移。
