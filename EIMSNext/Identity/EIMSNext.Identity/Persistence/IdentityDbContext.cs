using EIMSNext.Entities;
using EIMSNext.Identity.Interfaces;
using EIMSNext.Identity.Models;
using EIMSNext.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace EIMSNext.Identity.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options), IIdentityDbContext
{
    private DbSet<Client> ClientSet => Set<Client>();
    private DbSet<User> UserSet => Set<User>();
    private DbSet<UserCorp> UserCorpSet => Set<UserCorp>();
    private DbSet<Employee> EmployeeSet => Set<Employee>();
    private DbSet<IdentityLoginAudit> AuditSet => Set<IdentityLoginAudit>();
    private DbSet<PublicAccessSetting> PublicSettingSet => Set<PublicAccessSetting>();
    private DbSet<CorporateSettingReadModel> CorporateSettingSet => Set<CorporateSettingReadModel>();
    private DbSet<IntegrationLoginSetting> IntegrationLoginSettingSet => Set<IntegrationLoginSetting>();
    private DbSet<UserIntegrationBinding> UserIntegrationBindingSet => Set<UserIntegrationBinding>();

    public IQueryable<Client> Clients => ClientSet.AsNoTracking();
    public IQueryable<User> Users => UserSet.AsNoTracking();
    public IQueryable<UserCorp> UserCorps => UserCorpSet.AsNoTracking();
    public IQueryable<EmployeeLookup> Employees => EmployeeSet.AsNoTracking().Select(x => new EmployeeLookup { Id = x.Id, CorpId = x.CorpId ?? string.Empty, UserId = x.UserId, Code = x.Code, Status = x.Status });
    public IQueryable<PublicAccessSetting> PublicSettings => PublicSettingSet.AsNoTracking();
    public IQueryable<CorporateSettingReadModel> CorporateSettings => CorporateSettingSet.AsNoTracking();
    public IQueryable<IntegrationLoginSetting> IntegrationLoginSettings => IntegrationLoginSettingSet.AsNoTracking();
    public IQueryable<UserIntegrationBinding> UserIntegrationBindings => UserIntegrationBindingSet.AsNoTracking();

    public Task AddClient(Client entity) => AddAndSaveAsync(ClientSet, entity);
    public Task UpdateClient(Client entity) => UpdateAndSaveAsync(ClientSet, entity);
    public Task AddUser(User entity) => AddAndSaveAsync(UserSet, entity);
    public Task UpdateUser(User entity) => UpdateAndSaveAsync(UserSet, entity);
    public Task AddIdentityLoginAudit(IdentityLoginAudit entity) => AddAndSaveAsync(AuditSet, entity);
    public async Task AddIdentityLoginAudits(IReadOnlyCollection<IdentityLoginAudit> entities, CancellationToken cancellationToken = default)
    {
        foreach (var entity in entities)
        {
            var current = await AuditSet.FirstOrDefaultAsync(x => x.Id == entity.Id, cancellationToken);
            if (current is null) AuditSet.Add(entity); else Entry(current).CurrentValues.SetValues(entity);
        }
        await SaveChangesAsync(cancellationToken);
    }

    public Task AddIntegrationLoginSetting(IntegrationLoginSetting entity) => AddAndSaveAsync(IntegrationLoginSettingSet, entity);
    public Task UpdateIntegrationLoginSetting(IntegrationLoginSetting entity) => UpdateAndSaveAsync(IntegrationLoginSettingSet, entity);
    public Task AddUserIntegrationBinding(UserIntegrationBinding entity) => AddAndSaveAsync(UserIntegrationBindingSet, entity);
    public Task UpdateUserIntegrationBinding(UserIntegrationBinding entity) => UpdateAndSaveAsync(UserIntegrationBindingSet, entity);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 先把本上下文的实体显式登记进模型。
        // 本类的 DbSet 属性刻意是 private（对外只暴露 AsNoTracking 的 IQueryable），
        // EF 的 DbSet 自动发现不会拾取它们；而共享约定里「仅当实体已在模型中时才配置」
        // 的判断依赖实体已经在模型中——顺序颠倒的话 jsonb 转换器一个都不会挂上，
        // Client.Secrets 之类的内嵌对象就会被当成独立实体去要主键，宿主一启动即崩。
        modelBuilder.Entity<Client>();
        modelBuilder.Entity<User>();
        modelBuilder.Entity<Employee>();
        modelBuilder.Entity<UserCorp>();
        modelBuilder.Entity<IdentityLoginAudit>();
        modelBuilder.Entity<PublicAccessSetting>();
        modelBuilder.Entity<IntegrationLoginSetting>();
        modelBuilder.Entity<UserIntegrationBinding>();

        // 与业务主上下文共用同一份模型约定（表名、主键、jsonb、审计字段、软删除）。
        modelBuilder.ApplyEIMSNextModel();

        // UserCorp 是用户与企业的归属关系表，由业务主上下文负责写入；身份宿主只读，
        // 用于签发 token 时确定用户的默认企业（原先读的是内嵌 jsonb 投影 User.Crops）。
        modelBuilder.Entity<UserCorp>().ToTable("UserCorp");

        // 约定里按 ClrType.Name 定的表名在这里按实际表名覆盖（后配置者生效）。
        modelBuilder.Entity<Client>().ToTable("Client");
        modelBuilder.Entity<User>().ToTable("User");
        modelBuilder.Entity<Employee>().ToTable("Employee");
        modelBuilder.Entity<IdentityLoginAudit>().ToTable("IdentityLoginAudit");
        modelBuilder.Entity<PublicAccessSetting>().ToTable("PublicSetting");
        modelBuilder.Entity<CorporateSettingReadModel>().HasNoKey().ToView("CorporateSetting");

        // 第三方集成登录配置与绑定关系：由身份宿主写库，沿用 Postgres 存储。
        modelBuilder.Entity<IntegrationLoginSetting>().ToTable("IntegrationLoginSetting");
        modelBuilder.Entity<UserIntegrationBinding>().ToTable("UserIntegrationBinding");

        // ExtraParameters 是对象集合，必须显式落 jsonb（复杂对象/集合不能当独立实体要主键）。
        modelBuilder.Entity<IntegrationLoginSetting>()
            .Property(x => x.ExtraParameters)
            .HasConversion(JsonbValueConverter.Create<IList<IntegrationLoginSettingItem>>())
            .HasColumnType("jsonb");

        // PublicAccessSetting 是身份宿主自己的分区设置形状，与业务侧 PublicSetting 实体共用
        // "PublicSetting" 表；两个内嵌对象同样必须落 jsonb，理由同上。
        modelBuilder.Entity<PublicAccessSetting>().Property(x => x.Form)
            .HasConversion(JsonbValueConverter.Create<PublicFormAccessSetting>()).HasColumnType("jsonb");
        modelBuilder.Entity<PublicAccessSetting>().Property(x => x.Dashboard)
            .HasConversion(JsonbValueConverter.Create<PublishSection>()).HasColumnType("jsonb");
    }

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        => configurationBuilder.ConfigureEIMSNextConventions();
    private async Task AddAndSaveAsync<TEntity>(DbSet<TEntity> set, TEntity entity) where TEntity : class { set.Add(entity); await SaveChangesAsync(); }
    private async Task UpdateAndSaveAsync<TEntity>(DbSet<TEntity> set, TEntity entity) where TEntity : class { set.Update(entity); await SaveChangesAsync(); }
}
