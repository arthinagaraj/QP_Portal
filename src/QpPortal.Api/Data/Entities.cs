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
public class Setting
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

public class QpTemplate
{
    public int Id { get; set; }
    public int RegulationId { get; set; }
    public Regulation Regulation { get; set; } = null!;
    public string ExamType { get; set; } = "End Semester";
    public string Name { get; set; } = "";
    public string Duration { get; set; } = "3 Hours";
    public decimal MaxMarks { get; set; }
    public string? Instructions { get; set; }
    public int Version { get; set; } = 1;
    public string Status { get; set; } = "Draft";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<QpTemplatePart> Parts { get; set; } = new();
}

public class QpTemplatePart
{
    public int Id { get; set; }
    public int TemplateId { get; set; }
    public string PartName { get; set; } = "";
    public string Title { get; set; } = "";
    public string Distribution { get; set; } = "PerUnit";
    public int QuestionCount { get; set; }
    public decimal MarksEach { get; set; }
    public bool IsEitherOr { get; set; }
    public int DisplayOrder { get; set; }
}

public class Assignment
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public int SetterId { get; set; }
    public User Setter { get; set; } = null!;
    public int TemplateId { get; set; }
    public QpTemplate Template { get; set; } = null!;
    public string ExamSession { get; set; } = "";
    public DateOnly DueDate { get; set; }
    public string Status { get; set; } = "Assigned";
    public string LinkToken { get; set; } = "";
    public int AssignedBy { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? InviteSentAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
}