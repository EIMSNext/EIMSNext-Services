using EIMSNext.Core.Entities;
using EIMSNext.Entities;
using Microsoft.EntityFrameworkCore;

namespace EIMSNext.Persistence.PostgreSql;

/// <summary>
/// EIMSNext 业务主上下文（PostgreSQL / EF Core）。
/// </summary>
/// <remarks>
/// <para>
/// <b>本上下文是 PostgreSQL 表结构的唯一依据。</b>
/// <c>ApiHost/EIMSNext.Tool.DbMaintenance/Sql</c> 下的基线脚本按这里的模型投影生成
/// （生成器见 <see cref="PostgreSqlBaselineScript"/>），两者的差异由
/// <c>Tests/EIMSNext.Core.Tests</c> 的 <c>SchemaConsistencyTests</c> 与 <c>BaselineScriptTests</c> 守着：
/// 要改表结构，先改实体与本上下文，再跑
/// <c>dotnet test Tests/EIMSNext.Core.Tests --filter RegenerateBaselineScripts</c>
/// （需先置 <c>EIMS_REGENERATE_BASELINE=1</c>），不要在迁移链尾部追加「改列」脚本。
/// </para>
/// <para>
/// 表名、主键、jsonb 映射、软删除过滤等约定集中在
/// <see cref="EIMSNextModelConfiguration"/>，与身份宿主的上下文共用同一份，
/// 避免同一张表被不同宿主理解成不同形状。
/// </para>
/// </remarks>
public sealed class PostgreSqlDbContext(DbContextOptions<PostgreSqlDbContext> options) : DbContext(options)
{
    public DbSet<Corporate> Corporates => Set<Corporate>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserCorp> UserCorps => Set<UserCorp>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<EmployeeDepartment> EmployeeDepartments => Set<EmployeeDepartment>();
    public DbSet<EmployeeGroupMember> EmployeeGroupMembers => Set<EmployeeGroupMember>();
    public DbSet<EmployeeGroupCategory> EmployeeGroupCategories => Set<EmployeeGroupCategory>();
    public DbSet<EmployeeGroup> EmployeeGroups => Set<EmployeeGroup>();
    public DbSet<TenantAdminGroup> TenantAdminGroups => Set<TenantAdminGroup>();
    public DbSet<CorporateSetting> CorporateSettings => Set<CorporateSetting>();
    public DbSet<CrossBinding> CrossBindings => Set<CrossBinding>();
    public DbSet<FormDef> FormDefs => Set<FormDef>();
    public DbSet<FormData> FormDatas => Set<FormData>();
    public DbSet<FormListView> FormListViews => Set<FormListView>();
    public DbSet<FormNotify> FormNotifies => Set<FormNotify>();
    public DbSet<PrintDef> PrintDefs => Set<PrintDef>();
    public DbSet<AppDef> AppDefs => Set<AppDef>();
    public DbSet<AppProfile> AppProfiles => Set<AppProfile>();
    public DbSet<DashboardDef> DashboardDefs => Set<DashboardDef>();
    public DbSet<DashboardItemDef> DashboardItemDefs => Set<DashboardItemDef>();
    public DbSet<Wf_Definition> WfDefinitions => Set<Wf_Definition>();
    public DbSet<Wf_Task> WfTasks => Set<Wf_Task>();
    public DbSet<Wf_TaskLog> WfTaskLogs => Set<Wf_TaskLog>();
    public DbSet<Wf_ExecLog> WfExecLogs => Set<Wf_ExecLog>();
    public DbSet<EventFlowNodeExecution> EventFlowNodeExecutions => Set<EventFlowNodeExecution>();
    public DbSet<WorkflowTransitionExecution> WorkflowTransitionExecutions => Set<WorkflowTransitionExecution>();
    public DbSet<EventFlowScheduleItem> EventFlowScheduleItems => Set<EventFlowScheduleItem>();
    public DbSet<Ef_RunLog> EfRunLogs => Set<Ef_RunLog>();
    public DbSet<Ef_RunLogNode> EfRunLogNodes => Set<Ef_RunLogNode>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<ClientGrant> ClientGrants => Set<ClientGrant>();
    public DbSet<IdentityLoginAudit> IdentityLoginAudits => Set<IdentityLoginAudit>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<SystemMessage> SystemMessages => Set<SystemMessage>();
    public DbSet<Webhook> Webhooks => Set<Webhook>();
    public DbSet<WebhookAlias> WebhookAliases => Set<WebhookAlias>();
    public DbSet<WorkbenchConfig> WorkbenchConfigs => Set<WorkbenchConfig>();
    public DbSet<WorkbenchFavorite> WorkbenchFavorites => Set<WorkbenchFavorite>();
    public DbSet<WorkbenchRecentVisit> WorkbenchRecentVisits => Set<WorkbenchRecentVisit>();
    public DbSet<SerialNoSequence> SerialNoSequences => Set<SerialNoSequence>();
    public DbSet<PublicSetting> PublicSettings => Set<PublicSetting>();
    public DbSet<ExportLog> ExportLogs => Set<ExportLog>();
    public DbSet<Outbox.OutboxMessage> OutboxMessages => Set<Outbox.OutboxMessage>();
    public DbSet<Outbox.ProcessedMessage> ProcessedMessages => Set<Outbox.ProcessedMessage>();

    // ------------------------------------------------------------ 补齐遗漏的实体
    // 以下实体在业务层已通过 IRepository<> / IRepository 解析辅助方法实际使用，
    // 但此前没有登记到本上下文。DbRepository<T> 一律走 Context.Set<T>()，
    // 未登记的 T 会在第一次访问时抛
    // “Cannot create a DbSet for 'X' because this type is not included in the model for the context.”，
    // 因此这是运行期阻断级缺口，不是可选的完整性补充。
    // 溯源口径：扫描全仓（排除 Tests）中 IRepository<> / IService<> / IRepository 解析辅助方法 /
    // EntityServiceBase<> / ApiServiceBase<> 的泛型实参，与 DbSet 声明取差集。
    public DbSet<AppTemplate> AppTemplates => Set<AppTemplate>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<CorpOnboardingRequest> CorpOnboardingRequests => Set<CorpOnboardingRequest>();
    public DbSet<DashboardTemplate> DashboardTemplates => Set<DashboardTemplate>();
    public DbSet<DashboardItemTemplate> DashboardItemTemplates => Set<DashboardItemTemplate>();
    public DbSet<ECoinPrice> ECoinPrices => Set<ECoinPrice>();
    public DbSet<EventFlowHookSample> EventFlowHookSamples => Set<EventFlowHookSample>();
    public DbSet<FormTemplate> FormTemplates => Set<FormTemplate>();
    public DbSet<FormDataChangeLog> FormDataChangeLogs => Set<FormDataChangeLog>();
    public DbSet<FormDataImportLog> FormDataImportLogs => Set<FormDataImportLog>();
    public DbSet<FormDataPermissionGroup> FormDataPermissionGroups => Set<FormDataPermissionGroup>();
    public DbSet<FormDataPermissionGroupTemplate> FormDataPermissionGroupTemplates => Set<FormDataPermissionGroupTemplate>();
    public DbSet<FormNotifyScheduleItem> FormNotifyScheduleItems => Set<FormNotifyScheduleItem>();
    public DbSet<FormNotifyDispatchLog> FormNotifyDispatchLogs => Set<FormNotifyDispatchLog>();
    public DbSet<PluginProfile> PluginProfiles => Set<PluginProfile>();
    public DbSet<PluginInstall> PluginInstalls => Set<PluginInstall>();
    public DbSet<PrintDefTemplate> PrintDefTemplates => Set<PrintDefTemplate>();
    public DbSet<UploadedFile> UploadedFiles => Set<UploadedFile>();
    public DbSet<WebPushLog> WebPushLogs => Set<WebPushLog>();
    public DbSet<WfDefinitionTemplate> WfDefinitionTemplates => Set<WfDefinitionTemplate>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyEIMSNextModel();
    }

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        => configurationBuilder.ConfigureEIMSNextConventions();
}
