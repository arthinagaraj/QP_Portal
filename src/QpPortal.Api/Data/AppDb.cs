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
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<QpTemplate> QpTemplates => Set<QpTemplate>();
    public DbSet<QpTemplatePart> QpTemplateParts => Set<QpTemplatePart>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<BankDetail> BankDetails => Set<BankDetail>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<ClaimRate> ClaimRates => Set<ClaimRate>();
    public DbSet<QpClaim> Claims => Set<QpClaim>();
    public DbSet<Scrutiny> Scrutinies => Set<Scrutiny>();

    protected override void ConfigureConventions(ModelConfigurationBuilder c) =>
        c.Properties<decimal>().HavePrecision(10, 2);

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<UserRole>().HasKey(x => new { x.UserId, x.RoleId });
        b.Entity<RolePermission>().HasKey(x => new { x.RoleId, x.PermissionId });
        b.Entity<Course>().HasMany(c => c.Units).WithOne().HasForeignKey(u => u.CourseId);
        b.Entity<Setting>().HasKey(x => x.Key);
        b.Entity<QpTemplate>().Property(x => x.MaxMarks).HasPrecision(5, 1);
        b.Entity<QpTemplate>().HasMany(t => t.Parts).WithOne().HasForeignKey(p => p.TemplateId);
        b.Entity<QpTemplatePart>().Property(x => x.MarksEach).HasPrecision(5, 1);
        b.Entity<Assignment>().HasOne(a => a.Setter).WithMany().HasForeignKey(a => a.SetterId);
        b.Entity<BankDetail>().HasKey(x => x.UserId);
        b.Entity<Question>().Property(x => x.Marks).HasPrecision(5, 1);
        b.Entity<Question>().Property(x => x.MatchScore).HasPrecision(4, 2);
        b.Entity<ClaimRate>().HasKey(x => x.SetterType);
        b.Entity<QpClaim>().HasKey(x => x.AssignmentId);
    }
}