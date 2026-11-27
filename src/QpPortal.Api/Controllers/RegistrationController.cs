using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QpPortal.Api.Auth;
using QpPortal.Api.Data;

namespace QpPortal.Api.Controllers;

[ApiController]
[Route("api")]
public class RegistrationController(AppDb db) : ControllerBase
{
    public record RegisterRequest(string Name, string Email, string Mobile, string DateOfBirth,
        string? Designation, string? Department, string? Institution, string SetterType,
        int? ExperienceYears, string? Qualification, string? AreaOfInterest);
    public record RejectRequest(string Reason);

    // Public: "Register as QP setter" on the home page
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Email))
            return BadRequest("Name and e-mail are required.");
        if (!Regex.IsMatch(req.Mobile ?? "", @"^\d{10}$")) return BadRequest("Mobile must be 10 digits.");
        if (!DateOnly.TryParseExact(req.DateOfBirth, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dob))
            return BadRequest("Date of birth must be YYYY-MM-DD.");
        if (req.SetterType != "Internal" && req.SetterType != "External")
            return BadRequest("Choose Internal or External.");
        if (await db.Users.AnyAsync(u => u.Email == req.Email.Trim() || u.Mobile == req.Mobile))
            return Conflict("This e-mail or mobile number is already registered.");

        var setterRoleId = (await db.Roles.SingleAsync(r => r.Name == "Setter")).Id;
        db.Users.Add(new User
        {
            Name = req.Name.Trim(), Email = req.Email.Trim(), Mobile = req.Mobile, DateOfBirth = dob,
            Designation = req.Designation, Department = req.Department, Institution = req.Institution,
            SetterType = req.SetterType, ExperienceYears = req.ExperienceYears,
            Qualification = req.Qualification, AreaOfInterest = req.AreaOfInterest,
            RegistrationStatus = "Pending", Source = "Register",
            UserRoles = [new UserRole { RoleId = setterRoleId }]
        });
        await db.SaveChangesAsync();
        return Ok(new { message = "Registration received. You can log in once the exam cell approves it." });
    }

    // CoE / DCoE: list registrations (default: Pending)
    [HttpGet("registrations")]
    [Authorize(Policy = Perms.FacultyApprove)]
    public async Task<IActionResult> List(string status = "Pending") =>
        Ok(await db.Users
            .Where(u => u.Source == "Register" && u.RegistrationStatus == status)
            .OrderBy(u => u.Id)
            .Select(u => new { u.Id, u.Name, u.Email, u.Mobile, u.Designation, u.Department, u.Institution,
                               u.SetterType, u.ExperienceYears, u.Qualification, u.AreaOfInterest, u.RegistrationStatus })
            .ToListAsync());

    [HttpPost("registrations/{id:int}/approve")]
    [Authorize(Policy = Perms.FacultyApprove)]
    public Task<IActionResult> Approve(int id) => Decide(id, "Approved", null);

    [HttpPost("registrations/{id:int}/reject")]
    [Authorize(Policy = Perms.FacultyApprove)]
    public Task<IActionResult> Reject(int id, RejectRequest req) => Decide(id, "Rejected", req.Reason);

    private async Task<IActionResult> Decide(int id, string status, string? reason)
    {
        var u = await db.Users.FindAsync(id);
        if (u is null || u.Source != "Register") return NotFound();
        if (u.RegistrationStatus != "Pending") return BadRequest($"Already {u.RegistrationStatus}.");
        u.RegistrationStatus = status;
        u.RejectionReason = reason;
        u.ReviewedBy = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        u.ReviewedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { u.Id, u.Name, u.RegistrationStatus });
    }
}