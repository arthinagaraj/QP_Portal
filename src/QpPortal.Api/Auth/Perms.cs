namespace QpPortal.Api.Auth;

public static class Perms
{
    public const string SetterUpload       = "setter.upload";
    public const string FacultyApprove     = "faculty.approve";
    public const string CourseUpload       = "course.upload";
    public const string InstructionsManage = "instructions.manage";
    public const string TemplateManage     = "template.manage";
    public const string SetterAssign       = "setter.assign";
    public const string ReportView         = "report.view";
    public const string ClaimExport        = "claim.export";
    public const string PaperEdit          = "paper.edit";

    public static readonly string[] All =
    [
        SetterUpload, FacultyApprove, CourseUpload, InstructionsManage, TemplateManage,
        SetterAssign, ReportView, ClaimExport, PaperEdit
    ];
}