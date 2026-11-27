using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QpPortal.Api.Auth;
using QpPortal.Api.Data;
using QpPortal.Api.Services;

namespace QpPortal.Api.Controllers;

// Scrutiny workflow: CoE sends a submitted QP to a DCoE / HoD; reviewer approves or returns remarks
[ApiController]
[Route("api/scrutiny")]
[Authorize]
public class ScrutinyController(AppDb db, EmailService email, IConfiguration cfg) : ControllerBase
{
    int Me => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public record SendDto(int AssignmentId, int ReviewerId);
    public record ReviewDto(string Status, string? Remarks);
    static readonly string[] ReviewerRoles = ["DCoE", "HoD"];

    // people who can be chosen as reviewers
    [HttpGet("reviewers"), Authorize(Policy = Perms.ScrutinyAssign)]
    public async Task<IActionResult> Reviewers() => Ok(await (
        from ur in db.UserRoles
        join r in db.Roles on ur.RoleId equals r.Id
        join u in db.Users on ur.UserId equals u.Id
        where ReviewerRoles.Contains(r.Name) && u.IsActive
        orderby r.Name, u.Name
        select new { u.Id, u.Name, u.Department, Role = r.Name }).ToListAsync());

    // all scrutinies (latest per paper is shown in Analysis)
    [HttpGet, Authorize(Policy = Perms.ScrutinyAssign)]
    public async Task<IActionResult> All() => Ok(await (
        from s in db.Scrutinies
        join u in db.Users on s.ReviewerId equals u.Id
        orderby s.AssignedAt descending
        select new { s.Id, s.AssignmentId, s.Status, s.Remarks, s.AssignedAt, s.ReviewedAt, Reviewer = u.Name }).ToListAsync());

    [HttpPost, Authorize(Policy = Perms.ScrutinyAssign)]
    public async Task<IActionResult> Send(SendDto dto)
    {
        var a = await db.Assignments.Include(x => x.Course).FirstOrDefaultAsync(x => x.Id == dto.AssignmentId);
        if (a is null) return NotFound();
        if (a.Status != "Submitted") return BadRequest("Only submitted papers can be sent for scrutiny.");
        var reviewer = await db.Users.FindAsync(dto.ReviewerId);
        if (reviewer is null) return BadRequest("Unknown reviewer.");
        if (await db.Scrutinies.AnyAsync(s => s.AssignmentId == a.Id && s.ReviewerId == reviewer.Id && s.Status == "Pending"))
            return BadRequest($"Already with {reviewer.Name} for scrutiny.");

        db.Scrutinies.Add(new Scrutiny { AssignmentId = a.Id, ReviewerId = reviewer.Id, AssignedBy = Me });
        await db.SaveChangesAsync();

        // email has no question text – only a link to log in
        try
        {
            await email.SendAsync(reviewer.Email, reviewer.Name, $"QP scrutiny: {a.Course.Code} {a.Course.Title}",
                $"<p>Dear {reviewer.Name},</p><p>The question paper for <b>{a.Course.Code} – {a.Course.Title}</b> ({a.ExamSession}) " +
                $"has been sent to you for scrutiny.</p><p>Please log in to the QP Portal and open the <b>Scrutiny</b> tab: " +
                $"<a href=\"{cfg["App:BaseUrl"]}\">{cfg["App:BaseUrl"]}</a></p><p>Controller of Examinations</p>");
        }
        catch { /* mail failure must not block the workflow */ }
        return Ok();
    }

    // reviewer: my scrutiny list
    [HttpGet("mine"), Authorize(Policy = Perms.ScrutinyReview)]
    public async Task<IActionResult> Mine() => Ok(await (
        from s in db.Scrutinies
        join a in db.Assignments on s.AssignmentId equals a.Id
        join c in db.Courses on a.CourseId equals c.Id
        where s.ReviewerId == Me
        orderby s.Status == "Pending" descending, s.AssignedAt descending
        select new { s.Id, s.Status, s.Remarks, s.AssignedAt, s.ReviewedAt, c.Code, c.Title, a.ExamSession }).ToListAsync());

    // reviewer: read-only paper
    [HttpGet("{sid:int}/paper"), Authorize(Policy = Perms.ScrutinyReview)]
    public async Task<IActionResult> Paper(int sid)
    {
        var s = await db.Scrutinies.FindAsync(sid);
        if (s is null || s.ReviewerId != Me) return NotFound();
        var r = await PapersController.Load(db, s.AssignmentId);
        if (r is null) return NotFound();
        return Ok(new { Scrutiny = new { s.Id, s.Status, s.Remarks }, Paper = PapersController.PaperView(r.Value.A, r.Value.Parts, showSetter: false) });
    }

    [HttpPost("{sid:int}/review"), Authorize(Policy = Perms.ScrutinyReview)]
    public async Task<IActionResult> Review(int sid, ReviewDto dto)
    {
        var s = await db.Scrutinies.FindAsync(sid);
        if (s is null || s.ReviewerId != Me) return NotFound();
        if (dto.Status is not ("Approved" or "ChangesRequested")) return BadRequest("Status must be Approved or ChangesRequested.");
        if (dto.Status == "ChangesRequested" && string.IsNullOrWhiteSpace(dto.Remarks)) return BadRequest("Please enter the changes required.");
        s.Status = dto.Status; s.Remarks = dto.Remarks?.Trim(); s.ReviewedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok();
    }
}
