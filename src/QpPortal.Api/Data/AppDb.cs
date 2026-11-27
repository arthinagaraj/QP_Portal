using Microsoft.EntityFrameworkCore;

namespace QpPortal.Api.Data;

public class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Regulation> Regulations => Set<Regulation>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<SyllabusUnit> SyllabusUnits => Set<SyllabusUnit>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<UserRole>().HasKey(x => new { x.UserId, x.RoleId });
        b.Entity<RolePermission>().HasKey(x => new { x.RoleId, x.PermissionId });
        b.Entity<Course>().HasMany(c => c.Units).WithOne().HasForeignKey(u => u.CourseId);
    }
}