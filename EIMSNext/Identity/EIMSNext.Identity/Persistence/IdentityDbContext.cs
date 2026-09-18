using EIMSNext.Entities;
using EIMSNext.Identity.Interfaces;
using EIMSNext.Identity.Models;
using Microsoft.EntityFrameworkCore;

namespace EIMSNext.Identity.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options), IIdentityDbContext
{
    private DbSet<Client> ClientSet => Set<Client>();
    private DbSet<User> UserSet => Set<User>();
    private DbSet<Employee> EmployeeSet => Set<Employee>();
    private DbSet<IdentityLoginAudit> AuditSet => Set<IdentityLoginAudit>();
    private DbSet<PublicAccessSetting> PublicSettingSet => Set<PublicAccessSetting>();
    private DbSet<CorporateSettingReadModel> CorporateSettingSet => Set<CorporateSettingReadModel>();
    public IQueryable<Client> Clients => ClientSet.AsNoTracking();
    public IQueryable<User> Users => UserSet.AsNoTracking();
    public IQueryable<EmployeeLookup> Employees => EmployeeSet.AsNoTracking().Select(x => new EmployeeLookup { Id = x.Id, CorpId = x.CorpId ?? string.Empty, UserId = x.UserId, Code = x.Code, Status = x.Status });
    public IQueryable<PublicAccessSetting> PublicSettings => PublicSettingSet.AsNoTracking();
    public IQueryable<CorporateSettingReadModel> CorporateSettings => CorporateSettingSet.AsNoTracking();
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
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Client>().ToTable("Client"); modelBuilder.Entity<User>().ToTable("User"); modelBuilder.Entity<Employee>().ToTable("Employee"); modelBuilder.Entity<IdentityLoginAudit>().ToTable("IdentityLoginAudit"); modelBuilder.Entity<PublicAccessSetting>().ToTable("PublicSetting"); modelBuilder.Entity<CorporateSettingReadModel>().HasNoKey().ToView("CorporateSetting");
    }
    private async Task AddAndSaveAsync<TEntity>(DbSet<TEntity> set, TEntity entity) where TEntity : class { set.Add(entity); await SaveChangesAsync(); }
    private async Task UpdateAndSaveAsync<TEntity>(DbSet<TEntity> set, TEntity entity) where TEntity : class { set.Update(entity); await SaveChangesAsync(); }
}
