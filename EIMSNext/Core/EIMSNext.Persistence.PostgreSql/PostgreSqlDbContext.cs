using EIMSNext.Entities;
using Microsoft.EntityFrameworkCore;

namespace EIMSNext.Persistence.PostgreSql;

public sealed class PostgreSqlDbContext(DbContextOptions<PostgreSqlDbContext> options) : DbContext(options)
{
    public DbSet<Corporate> Corporates => Set<Corporate>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserCorp> UserCorps => Set<UserCorp>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<EmployeeDepartment> EmployeeDepartments => Set<EmployeeDepartment>();
    public DbSet<FormDef> FormDefs => Set<FormDef>();
    public DbSet<FormData> FormDatas => Set<FormData>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes()) entity.SetTableName(entity.ClrType.Name);
        modelBuilder.Entity<FormData>().Property(x => x.Data).HasColumnType("jsonb");
        modelBuilder.Entity<FormDef>().Property(x => x.Content).HasColumnType("jsonb");
        modelBuilder.Entity<FormDef>().Property(x => x.FormSettings).HasColumnType("jsonb");
        modelBuilder.Entity<EmployeeDepartment>().HasIndex(x => new { x.CorpId, x.EmployeeId, x.DepartmentId }).IsUnique();
        modelBuilder.Entity<UserCorp>().HasIndex(x => new { x.UserId, x.CorpId }).IsUnique();
        modelBuilder.Entity<Employee>().HasIndex(x => new { x.CorpId, x.Code });
        modelBuilder.Entity<Department>().HasIndex(x => new { x.CorpId, x.Code });
    }
}
