using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QpPortal.Api.Auth;
using QpPortal.Api.Data;
using QpPortal.Api.Services;

namespace QpPortal.Api.Controllers;

[ApiController]
[Route("api/assignments")]
[Authorize(Policy = Perms.SetterAssign)]
public class AssignmentsController(AppDb db, EmailService email, IConfiguration config) : ControllerBase
{
    public record AssignRequest(int CourseId, int SetterId, string ExamSession, string DueDate, string? ExamType);

    [HttpGet]
    public async Task<IActionResult> List() =>
        Ok(await db.Assignments.OrderByDescending(a => a.Id).Select(a => new
        {
            a.Id, Course = a.Course.Code + " - " + a.Course.Title, Regulation = a.Course.Regulation.Code,
            Setter = a.Setter.Name, a.Setter.SetterType, a.ExamSession, a.DueDate, a.Status,
            a.InviteSentAt, a.SubmittedAt, Template = a.Template.Name + " v" + a.Template.Version
        }).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create(AssignRequest req)
    {
        if (!DateOnly.TryParse(req.DueDate, out var due) || due < DateOnly.FromDateTime(DateTime.Today))
            return BadRequest("Due date must be today or later (YYYY-MM-DD).");

        var course = await db.Courses.Include(c => c.Regulation).FirstOrDefaultAsync(c => c.Id == req.CourseId);
        if (course is null) return BadRequest("Unknown course.");

        var setter = await db.Users.FirstOrDefaultAsync(u => u.Id == req.SetterId && u.IsActive
            && u.RegistrationStatus == "Approved" && u.UserRoles.Any(r => r.Role.Name == "Setter"));
        if (setter is null) return BadRequest("Setter not found or not approved.");

        var examType = req.ExamType ?? "End Semester";
        var template = await db.QpTemplates.FirstOrDefaultAsync(t => t.RegulationId == course.RegulationId
                                                                    && t.ExamType == examType && t.Status == "Published");
        if (template is null)
            return BadRequest($"No published QP framework for {course.Regulation.Code} ({examType}). Ask CoE to publish one.");

        if (await db.Assignments.AnyAsync(a => a.CourseId == course.Id && a.ExamSession == req.ExamSession
                                            && a.Status != "Cancelled"))
            return Conflict("This subject already has a setter for this exam session.");

        var a = new Assignment
        {
            CourseId = course.Id, SetterId = setter.Id, TemplateId = template.Id, ExamSession = req.ExamSession,
            DueDate = due, LinkToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)),
            AssignedBy = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
        };
        db.Assignments.Add(a);
        await db.SaveChangesAsync();

        var (sent, error) = await SendInvite(a, course, setter);
        return Ok(new { a.Id, link = Link(a), emailSent = sent, emailError = error });
    }

    [HttpPost("{id:int}/resend")]
    public async Task<IActionResult> Resend(int id)
    {
        var a = await db.Assignments.Include(x => x.Course).Include(x => x.Setter).FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();
        var (sent, error) = await SendInvite(a, a.Course, a.Setter);
        return Ok(new { emailSent = sent, emailError = error });
    }

    string Link(Assignment a) => $"{config["App:BaseUrl"]}/setter?token={a.LinkToken}";

    async Task<(bool, string?)> SendInvite(Assignment a, Course course, User setter)
    {
        var instructions = (await db.Settings.FindAsync("CommonInstructions"))?.Value ?? "";
        var html = $"""
            <p>Dear {setter.Name},</p>
            <p>You have been appointed as Question Paper Setter for
               <b>{course.Code} - {course.Title}</b> ({a.ExamSession}).</p>
            <p><b>Last date for submission: {a.DueDate:dd-MMM-yyyy}</b></p>
            <p><a href="{Link(a)}">Open the QP Setting Portal</a></p>
            <p>Login with your registered mobile number or e-mail and your date of birth.</p>
            <h3>Instructions to the QP setter</h3>
            {instructions}
            <p>Controller of Examinations</p>
            """;
        try
        {
            await email.SendAsync(setter.Email, setter.Name, $"QP setting invitation: {course.Code} {course.Title}", html);
            a.InviteSentAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}