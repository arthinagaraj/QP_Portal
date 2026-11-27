namespace QpPortal.Api.Data;

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Mobile { get; set; }
    public string? PasswordHash { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Designation { get; set; }
    public string? Department { get; set; }
    public string? Institution { get; set; }
    public string? SetterType { get; set; }
    public string? Qualification { get; set; }
    public int? ExperienceYears { get; set; }
    public string? AreaOfInterest { get; set; }
    public string RegistrationStatus { get; set; } = "Approved";
    public string Source { get; set; } = "Upload";
    public string? RejectionReason { get; set; }
    public int? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public List<UserRole> UserRoles { get; set; } = new();
}

public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<RolePermission> RolePermissions { get; set; } = new();
}

public class Permission
{
    public int Id { get; set; }
    public string Key { get; set; } = "";
    public string Description { get; set; } = "";
}

public class UserRole
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
}

public class RolePermission
{
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public int PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}
public class Regulation
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public class Course
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public int RegulationId { get; set; }
    public Regulation Regulation { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public List<SyllabusUnit> Units { get; set; } = new();
}

public class SyllabusUnit
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public int UnitNo { get; set; }
    public string Title { get; set; } = "";
    public string CoText { get; set; } = "";
    public string Topics { get; set; } = "";
}