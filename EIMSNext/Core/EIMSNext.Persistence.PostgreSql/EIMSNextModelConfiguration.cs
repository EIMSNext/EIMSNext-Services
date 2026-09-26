using System.Linq.Expressions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Query;
using EIMSNext.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EIMSNext.Persistence.PostgreSql;

/// <summary>
/// EIMSNext 在 PostgreSQL 上的共享 EF Core 模型约定。
/// </summary>
/// <remarks>
/// <para>
/// 解决方案里不止一个 <see cref="DbContext"/>（业务主上下文 <see cref="PostgreSqlDbContext"/>、
/// 身份宿主 <c>IdentityDbContext</c>、工作流宿主 <c>WfDbContext</c>），它们映射同一批表。
/// 表名、主键类型、jsonb 映射、审计字段、软删除过滤这些约定必须一致，
/// 否则同一个库会被不同宿主理解成不同形状——身份宿主此前就漏掉了 jsonb 转换器，
/// 一构建模型就会抛 <c>The entity type 'X' requires a primary key to be defined</c>。
/// 这里把约定收成一份，供各宿主复用。
/// </para>
/// <para>
/// 所有按实体配置的项都先判断该实体是否在本上下文的模型里：上下文只会装载自己声明过的
/// 实体，不声明的不会被顺手带进来（例如身份宿主只需要 Client / User / Employee /
/// IdentityLoginAudit / PublicAccessSetting 五张表）。
/// </para>
/// </remarks>
public static class EIMSNextModelConfiguration
{
    /// <summary>标识符列（主键与外键）在 citext 之上再挂的排序规则：<c>C</c>，按字节比较。</summary>
    private const string IdentifierCollation = "C";

    /// <summary>
    /// 字符列的类型：<c>citext</c>，由
    /// <c>Sql/000_CreateCaseInsensitiveType.sql</c> 在库里创建。手写原生命令时也应把字符串参数
    /// 按这个类型发送，否则 PG 会把 citext 列降级成 <c>(col)::text</c> 比较（丢索引条件）。
    /// </summary>
    public const string CaseInsensitiveType = "citext";

    /// <summary>
    /// 注册跨上下文通用的属性转换约定。
    /// </summary>
    /// <param name="configurationBuilder">EF Core 的约定构建器。</param>
    public static void ConfigureEIMSNextConventions(this ModelConfigurationBuilder configurationBuilder)
    {
        // Operator 是审计字段的复合值对象，落库为 jsonb，不作为独立表；
        // FormData.Data（Dictionary<string, object?>，原 ExpandoObject）的转换器不能在这里注册：
        // Dictionary<string, object> 是 EF 属性包共享类型的 CLR 形状，预约定 Properties<>() 会抛
        // SharedTypeEntityType 异常，只能在 ApplyEIMSNextModel 里用 HasConversion 逐属性配置。
        configurationBuilder.Properties<Operator>().HaveConversion<OperatorJsonConverter>();

        // EF Core 默认落整数，会让历史数据 "Published" 读不回来，因此必须显式转换。
        configurationBuilder.Properties<Enum>().HaveConversion<string>();
    }

    /// <summary>
    /// 应用 EIMSNext 的表名、主键、jsonb 映射、软删除过滤与索引约定。
    /// </summary>
    /// <param name="modelBuilder">EF Core 的模型构建器。</param>
    /// <remarks>
    /// 调用后仍可在具体上下文里用 <c>ToTable</c> 覆盖表名（后配置者生效），
    /// 身份宿主就是用它把 <c>PublicAccessSetting</c> 映到 <c>"PublicSetting"</c> 表的。
    /// </remarks>
    public static void ApplyEIMSNextModel(this ModelBuilder modelBuilder)
    {
        // ------------------------------------------------------------ 表名
        // 表名规则：精确实体名单数，不复数化、不加前缀。PostgreSQL 下由 EF 自动加引号，
        // 因此 C# 的 FormData 对应 SQL 里的 "FormData"，与迁移脚本保持一致。
        // Wf_* / Ef_* 的类名本身就带下划线前缀，直接沿用即可（见项目既有约定）。
        foreach (var entity in modelBuilder.Model.GetEntityTypes()) entity.SetTableName(entity.ClrType.Name);

        // ------------------------------------------------------------ 自定义 SQL 函数
        // jsonb 内部键的取值/匹配必须落到 SQL 函数（或运算符）上，EF Core 才能翻译
        // （ExpandoObject 的索引器没法变成 JSONPath 访问）。
        // 各宿主上下文都要注册：身份宿主、工作流宿主同样会查 jsonb 内部键。
        modelBuilder.HasDbFunction(() => PgJsonFunctions.JsonPath(default!, default!))
            .HasName("eims_json_text")
            .HasSchema("public")
            .HasParameter("json").HasStoreType("jsonb");

        // JsonMatch 翻译成 `jsonb @? jsonpath` 运算符而不是 eims_json_match 函数调用：
        // GIN 索引（jsonb_ops / jsonb_path_ops）只服务运算符谓词，函数调用只能是 Seq Scan
        // （实测 PostgreSQL 18：@? 走 Bitmap Index Scan，jsonb_path_exists 是 Seq Scan）。
        // HasDbFunction 的自定义 Translation 会在 IMethodCallTranslatorPlugin 之前被咨询
        // （RelationalMethodCallTranslatorProvider 先查 model.FindDbFunction），
        // 且 ReplaceService<IMethodCallTranslatorPlugin> 与 Npgsql 组合不可靠，因此走这条路。
        // SQL 渲染由 EimsNpgsqlQuerySqlGenerator（UseEimsJsonPathOperators 接线）完成。
        // 数据库侧的 eims_json_match 函数保留（007 脚本），供手工 SQL / 排查使用。
        modelBuilder.HasDbFunction(() => PgJsonFunctions.JsonMatch(default!, default!))
            .HasName("eims_json_match")
            .HasSchema("public")
            .HasTranslation(args => new JsonPathExistsExpression(args[0], args[1]))
            .HasParameter("json").HasStoreType("jsonb");

        // 排序专用：返回 jsonb 而不是 text，避免数字退化成字典序（"10" < "9"）。
        // CLR 侧声明为 string 只是表达式树的载体；该调用只出现在 ORDER BY 里，
        // 不会投影回客户端，因此不涉及 jsonb → string 的反序列化。
        modelBuilder.HasDbFunction(() => PgJsonFunctions.JsonSort(default!, default!))
            .HasName("eims_json_sort")
            .HasSchema("public")
            .HasParameter("json").HasStoreType("jsonb");

        // ------------------------------------------------------------ jsonb 映射
        // 计划第 1 条：结构化字段用 jsonb，既保留可查询性，也避免为嵌套结构建表。
        // 复杂对象与对象集合必须显式挂转换器，理由见 JsonbValueConverter 的说明。
        // ------------------------------------------------------------ 动态字典
        // FormData.Data（Dictionary<string, object?>，原 ExpandoObject）落 jsonb，读写都做
        // 「CLR 类型 ↔ jsonb」深度还原。必须走 PropertyBuilder.HasConversion：
        // 对 Dictionary<string, object> 形状的属性，直接在元数据上 SetValueConverter 会被
        // Npgsql 的动态 JSON 收尾约定覆盖成裸 jsonb 参数（写库时抛 InvalidCastException）。
        // 注意也不能用预约定 Properties<Dictionary<string, object?>>()——那是 EF 属性包
        // 共享类型的 CLR 形状，会在建模阶段抛 SharedTypeEntityType 异常。
        Mapped<FormData>(modelBuilder)?.Property(x => x.Data)
            .HasConversion(new DynamicJsonbValueConverter())
            .HasColumnType("jsonb");
        Mapped<FormDef>(modelBuilder)?.Property(x => x.Content).HasConversion(Jsonb<FormContent>()).HasColumnType("jsonb");
        Mapped<FormDef>(modelBuilder)?.Property(x => x.FormSettings).HasConversion(Jsonb<FormSettings>()).HasColumnType("jsonb");
        Mapped<FormDef>(modelBuilder)?.Property(x => x.PublicRelatedFormIds).HasConversion(Jsonb<List<string>>()).HasColumnType("jsonb");
        Mapped<AppDef>(modelBuilder)?.Property(x => x.HomeEntryIds).HasConversion(Jsonb<List<string>>()).HasColumnType("jsonb");
        Mapped<AppDef>(modelBuilder)?.Property(x => x.AppMenus).HasConversion(Jsonb<List<AppMenu>>()).HasColumnType("jsonb");
        Mapped<DashboardDef>(modelBuilder)?.Property(x => x.PublishMembers).HasConversion(Jsonb<List<Member>>()).HasColumnType("jsonb");

        // 必须显式挂转换器，否则 EF 会把 ClientGrantType / ClientSecret / ClientScope
        // 当成独立实体去要主键，模型校验阶段直接抛异常。
        // 注意 ClientScope 是 { Scope } 值对象，与旧脚本里同名的那张 "ClientScope" 表不是一回事
        //（C# 侧没有对应实体，该表已移除）。
        Mapped<Client>(modelBuilder)?.Property(x => x.ClientSecrets).HasConversion(Jsonb<List<ClientSecret>>()).HasColumnType("jsonb");
        Mapped<Client>(modelBuilder)?.Property(x => x.AllowedGrantTypes).HasConversion(Jsonb<List<ClientGrantType>>()).HasColumnType("jsonb");
        Mapped<Client>(modelBuilder)?.Property(x => x.AllowedScopes).HasConversion(Jsonb<List<ClientScope>>()).HasColumnType("jsonb");
        Mapped<ClientGrant>(modelBuilder)?.Property(x => x.AppIds).HasConversion(Jsonb<List<string>>()).HasColumnType("jsonb");
        Mapped<ClientGrant>(modelBuilder)?.Property(x => x.ResourceActions).HasConversion(Jsonb<List<ResourceActionGrant>>()).HasColumnType("jsonb");
        Mapped<ClientGrant>(modelBuilder)?.Property(x => x.IpWhitelist).HasConversion(Jsonb<List<string>>()).HasColumnType("jsonb");
        Mapped<WebhookAlias>(modelBuilder)?.Property(x => x.FieldAlias).HasConversion(Jsonb<List<FieldAliasItem>>()).HasColumnType("jsonb");

        // PublicSetting 的两个分区设置对象也是内嵌值对象；它们的子级
        // （PublicPublishSection 的 FormLink/DataLink/QueryLink/Wechat/ExtLink、字段权限集合等）
        // 都随这两个 jsonb 一起序列化，不会再被 EF 当成实体。
        Mapped<PublicSetting>(modelBuilder)?.Property(x => x.Form).HasConversion(Jsonb<PublicFormSetting>()).HasColumnType("jsonb");
        Mapped<PublicSetting>(modelBuilder)?.Property(x => x.Dashboard).HasConversion(Jsonb<PublicDashboardSetting>()).HasColumnType("jsonb");

        // 补齐遗漏实体时新增的内嵌集合/对象：同一原理，否则 EF 会把
        // DataChangeContent / FormFieldPermission / WfMetadata / EventSetting
        // 当成独立实体去要主键。
        Mapped<FormDataChangeLog>(modelBuilder)?.Property(x => x.Content).HasConversion(Jsonb<List<DataChangeContent>>()).HasColumnType("jsonb");
        Mapped<FormDataPermissionGroup>(modelBuilder)?.Property(x => x.FormFieldPermissions).HasConversion(Jsonb<List<FormFieldPermission>>()).HasColumnType("jsonb");
        Mapped<FormDataPermissionGroup>(modelBuilder)?.Property(x => x.Members).HasConversion(Jsonb<List<Member>>()).HasColumnType("jsonb");
        Mapped<FormDataPermissionGroupTemplate>(modelBuilder)?.Property(x => x.FormFieldPermissions).HasConversion(Jsonb<List<FormFieldPermission>>()).HasColumnType("jsonb");
        Mapped<FormTemplate>(modelBuilder)?.Property(x => x.Content).HasConversion(Jsonb<FormContent>()).HasColumnType("jsonb");
        Mapped<FormTemplate>(modelBuilder)?.Property(x => x.FormSettings).HasConversion(Jsonb<FormSettings>()).HasColumnType("jsonb");
        Mapped<WfDefinitionTemplate>(modelBuilder)?.Property(x => x.Metadata).HasConversion(Jsonb<WfMetadata>()).HasColumnType("jsonb");
        // EventSetting 是可空属性（EventSetting?），泛型实参必须带 ? 才能与
        // PropertyBuilder<EventSetting?>.HasConversion(ValueConverter<EventSetting?, …>) 精确匹配。
        Mapped<WfDefinitionTemplate>(modelBuilder)?.Property(x => x.EventSetting).HasConversion(Jsonb<EventSetting?>()).HasColumnType("jsonb");
        Mapped<Wf_Definition>(modelBuilder)?.Property(x => x.Metadata).HasConversion(Jsonb<WfMetadata>()).HasColumnType("jsonb");
        Mapped<Wf_Definition>(modelBuilder)?.Property(x => x.EventSetting).HasConversion(Jsonb<EventSetting?>()).HasColumnType("jsonb");
        Mapped<Wf_Task>(modelBuilder)?.Property(x => x.Starter).HasColumnType("jsonb");
        Mapped<Wf_Task>(modelBuilder)?.Property(x => x.DataBrief).HasConversion(Jsonb<List<BriefField>>()).HasColumnType("jsonb");
        Mapped<Wf_TaskLog>(modelBuilder)?.Property(x => x.Approver).HasColumnType("jsonb");
        Mapped<Wf_TaskLog>(modelBuilder)?.Property(x => x.DataBrief).HasConversion(Jsonb<List<BriefField>>()).HasColumnType("jsonb");
        Mapped<TenantAdminGroup>(modelBuilder)?.Property(x => x.EmployeeIds).HasConversion(Jsonb<List<string>>()).HasColumnType("jsonb");

        // ValueConverter 只负责序列化，不会告诉 EF 如何比较集合快照。
        // 为所有 JSONB 集合补结构比较器，避免对 List<T> 做 Add/Remove 后变更跟踪失效。
        foreach (var property in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(x => x.GetProperties())
                     .Where(x => x.GetValueConverter() is not null
                         && x.ClrType != typeof(string)
                         && typeof(System.Collections.IEnumerable).IsAssignableFrom(x.ClrType)))
        {
            property.SetValueComparer(JsonbValueConverter.CreateComparer(property.ClrType));
        }

        // ------------------------------------------------------------ 刻意 *不* 用 jsonb 的字符串列
        // 下面 6 个属性的 CLR 类型是 string，C# 侧把它当作「一段 JSON 文本」显式
        // SerializeToJson / DeserializeFromJson（见 EfDataProcessor、AppPublishService 等），
        // 从不交给数据库做 jsonb 解析，也没有任何 jsonb 索引或 -> / @> 查询依赖它们。
        // 若把它们声明成 jsonb，会引入两个真实故障：
        //   1) 空串非法："" 不是合法 JSON，实测 PostgreSQL 报
        //      invalid input syntax for type json / The input string ended unexpectedly。
        //      而 CorporateSettingService.Normalize 明确把 Value 归一化成 string.Empty，
        //      即「保存一条没填值的配置」必然 500。
        // 因此这里保持 text，与 CLR 类型一一对应；真正的结构化字段（FormData.Data、
        // FormDef.Content 等）仍然按 jsonb 映射，见上文。
        Mapped<CorporateSetting>(modelBuilder)?.Property(x => x.Value).HasColumnType("text");
        Mapped<WorkbenchConfig>(modelBuilder)?.Property(x => x.Layout).HasColumnType("text");
        Mapped<DashboardItemDef>(modelBuilder)?.Property(x => x.Details).HasColumnType("text");
        Mapped<PrintDef>(modelBuilder)?.Property(x => x.Content).HasColumnType("text");
        Mapped<Wf_Definition>(modelBuilder)?.Property(x => x.Content).HasColumnType("text");
        Mapped<EventFlowNodeExecution>(modelBuilder)?.Property(x => x.ResultSnapshot).HasColumnType("text");
        // WorkflowTransitionExecution.Error 存放异常描述，同样是普通文本。
        Mapped<WorkflowTransitionExecution>(modelBuilder)?.Property(x => x.Error).HasColumnType("text");

        // ------------------------------------------------------------ 字符列
        // 全部字符列（主键与外键、业务文本、以文本落库的枚举列，见 ConfigureEIMSNextConventions
        // 的 Properties<Enum>）统一用 citext：EF 会把对应参数一并按 citext 发送，等值 / IN / LIKE
        // 天然大小写无关且走索引，不必在 LINQ 里包 ToLower()，也不必在写路径折叠大小写。
        // 只挑当前已是字符型的列，避免把 jsonb 列改掉。
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                if (!IsTextualClrType(property.ClrType)) continue;
                if (!IsCharacterColumn(property.GetColumnType())) continue;

                property.SetColumnType(CaseInsensitiveType);

                // 标识符列再按字节比较：只承载 TSID，不需要语言学语义，且 TSID 靠 ASCII 序表达时间序，
                // 确定性字节序才是对的。取「名为 Id 或以 Id 结尾」而不是只枚举已声明关系的外键：
                // PG 比较两个排序规则不同的列会抛 could not determine which collation to use，
                // 而 GetForeignKeys() 拿不全纯标量外键，漏掉任一引用方只会等真实查询落到那张表时才炸。
                if (IsIdentifierName(property.Name)) property.SetCollation(IdentifierCollation);
            }
        }

        // ------------------------------------------------------------ 运算符值对象
        // Operator 是审计字段的复合值对象，落库为 jsonb，不作为独立表。
        // 注意：Operator 是引用类型，typeof(Operator?) 在 C# 里不成立（可空引用类型没有独立 Type），
        // 因此只比较 Operator 本身——引用类型的可空性由列的 nullability 表达。
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties().Where(x => x.ClrType == typeof(Operator)))
            {
                property.SetColumnType("jsonb");
            }
        }

        // ------------------------------------------------------------ 索引
        // 计划第 6 条：索引名不复数化，UX_ 唯一 / IX 普通，字段顺序与查询顺序一致。
        // 这里显式指定数据库索引名，使 EF 生成的建表脚本与手写索引脚本用同一套命名，
        // 避免同一组列上出现两个名字不同、语义相同的索引。
        Mapped<EmployeeDepartment>(modelBuilder)?.HasIndex(x => new { x.CorpId, x.EmployeeId, x.DepartmentId })
            .IsUnique().HasDatabaseName("UX_EmployeeDepartment_CorpId_EmployeeId_DepartmentId");
        Mapped<UserCorp>(modelBuilder)?.HasIndex(x => new { x.UserId, x.CorpId })
            .IsUnique().HasDatabaseName("UX_UserCorp_UserId_CorpId");
        Mapped<Employee>(modelBuilder)?.HasIndex(x => new { x.CorpId, x.Code })
            .HasDatabaseName("IX_Employee_CorpId_Code");
        Mapped<Department>(modelBuilder)?.HasIndex(x => new { x.CorpId, x.Code })
            .HasDatabaseName("IX_Department_CorpId_Code");
        Mapped<FormData>(modelBuilder)?.HasIndex(x => new { x.CorpId, x.FormId, x.DeleteFlag })
            .HasDatabaseName("IX_FormData_CorpId_FormId_DeleteFlag");
        Mapped<FormDef>(modelBuilder)?.HasIndex(x => new { x.CorpId, x.AppId })
            .HasDatabaseName("IX_FormDef_CorpId_AppId");

        // ------------------------------------------------------------ 关系（导航 + 外键）
        // 员工与部门 / 员工组是多对多中间表：Employee 通过 EmployeeId 外键导航到
        // EmployeeDepartment / EmployeeGroupMember，使 OData 的
        // Departments/any(...) / Groups/any(...) 可翻译为 SQL。
        // 这里显式声明，避免 EF 仅靠约定在「仅集合端有导航、依赖端无反向导航」时漏配外键
        // （那样 OData 的 any 过滤会抛无法翻译的异常）。
        // EmployeeDepartment → Department 的外键（DepartmentId）已由约定依据其 Department 导航生成。
        Mapped<Employee>(modelBuilder)?
            .HasMany(e => e.Departments).WithOne().HasForeignKey("EmployeeId");
        Mapped<Employee>(modelBuilder)?
            .HasMany(e => e.Groups).WithOne().HasForeignKey("EmployeeId");

        // ------------------------------------------------------------ 软删除过滤
        // DeleteFlag 是全局约定；把过滤下沉到模型层，避免每个查询都手写 where !DeleteFlag。
        // 需要读取已删除行时用 IgnoreQueryFilters()。
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(IDeleteFlag).IsAssignableFrom(entity.ClrType)) continue;
            var parameter = Expression.Parameter(entity.ClrType, "e");
            var property = Expression.Property(parameter, nameof(IDeleteFlag.DeleteFlag));
            var body = Expression.Not(property);
            modelBuilder.Entity(entity.ClrType).HasQueryFilter(Expression.Lambda(body, parameter));
        }
    }

    /// <summary>仅当实体已在本上下文的模型中时返回其构建器，否则返回 <c>null</c>。</summary>
    /// <returns>实体构建器或 <c>null</c>。</returns>
    private static EntityTypeBuilder<TEntity>? Mapped<TEntity>(ModelBuilder modelBuilder) where TEntity : class
        => modelBuilder.Model.FindEntityType(typeof(TEntity)) is null ? null : modelBuilder.Entity<TEntity>();

    /// <summary>
    /// 判断属性名是否属于标识符列（主键或引用主键的外键）。
    /// </summary>
    /// <remarks>
    /// 只认 <c>Id</c> 本身与 <c>XxxId</c> 形态；反过来不会被误伤的是 <c>Idle</c>、
    /// <c>Identifier</c> 这类同首字母的词——它们不是本模型的列，此处仍要求以 <c>Id</c> 收尾。
    /// </remarks>
    private static bool IsIdentifierName(string name)
        => string.Equals(name, "Id", StringComparison.Ordinal)
           || (name.EndsWith("Id", StringComparison.Ordinal) && name.Length > 2);

    /// <summary>
    /// 判断 CLR 类型是否可能落成字符列：<c>string</c> 或枚举（枚举经
    /// <c>Properties&lt;Enum&gt;().HaveConversion&lt;string&gt;()</c> 落成 text）。
    /// </summary>
    /// <remarks>
    /// 可空枚举的 <see cref="Type.IsEnum"/> 为 false，因此先剥掉 <see cref="Nullable{T}"/>。
    /// </remarks>
    private static bool IsTextualClrType(Type clrType)
    {
        var type = Nullable.GetUnderlyingType(clrType) ?? clrType;
        return type == typeof(string) || type.IsEnum;
    }

    /// <summary>
    /// 判断列类型是否为字符型（可换成 citext）。
    /// </summary>
    /// <remarks>
    /// 未显式声明列类型时（<see cref="Microsoft.EntityFrameworkCore.Metadata.IReadOnlyProperty.GetColumnType"/> 返回 <c>null</c>），
    /// string 属性按 Npgsql 约定映射为 <c>text</c>，同样算字符型。
    /// </remarks>
    private static bool IsCharacterColumn(string? columnType)
    {
        if (string.IsNullOrEmpty(columnType)) return true;

        return columnType.StartsWith("text", StringComparison.OrdinalIgnoreCase)
               || columnType.StartsWith("character", StringComparison.OrdinalIgnoreCase)
               || columnType.StartsWith("varchar", StringComparison.OrdinalIgnoreCase)
               || columnType.StartsWith("citext", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>生成 jsonb 值转换器，见 <see cref="JsonbValueConverter.Create{TValue}"/>。</summary>
    /// <remarks>
    /// 约束用 <c>class?</c>：可空属性（<c>EventSetting?</c>）需要
    /// <c>ValueConverter&lt;EventSetting?, string&gt;</c> 才能与
    /// <c>PropertyBuilder&lt;TProperty&gt;.HasConversion</c> 的签名精确匹配，否则报 CS8620。
    /// </remarks>
    private static ValueConverter<TValue, string> Jsonb<TValue>() where TValue : class?
        => JsonbValueConverter.Create<TValue>();
}
