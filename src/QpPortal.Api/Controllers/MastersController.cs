using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QpPortal.Api.Auth;
using QpPortal.Api.Data;

namespace QpPortal.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class MastersController(AppDb db) : ControllerBase
{
    [HttpGet("regulations")]
    public async Task<IActionResult> Regulations() =>
        Ok(await db.Regulations.OrderBy(r => r.Code).ToListAsync());

    [HttpGet("courses")]
    public async Task<IActionResult> Courses() =>
        Ok(await db.Courses.OrderBy(c => c.Code)
            .Select(c => new { c.Id, c.Code, c.Title, Regulation = c.Regulation.Code, Units = c.Units.Count })
            .ToListAsync());

    [HttpGet("courses/{code}")]
    public async Task<IActionResult> Course(string code)
    {
        var c = await db.Courses.Include(x => x.Regulation).Include(x => x.Units)
                                .FirstOrDefaultAsync(x => x.Code == code);
        if (c is null) return NotFound();
        return Ok(new
        {
            c.Id, c.Code, c.Title, Regulation = c.Regulation.Code,
            Units = c.Units.OrderBy(u => u.UnitNo).Select(u => new { u.UnitNo, u.Title, Co = u.CoText, u.Topics })
        });
    }

    [HttpGet("setters")]
    [Authorize(Policy = Perms.SetterAssign)]
    public async Task<IActionResult> Setters() =>
        Ok(await db.Users
            .Where(u => u.UserRoles.Any(r => r.Role.Name == "Setter"))
            .OrderBy(u => u.Name)
            .Select(u => new { u.Id, u.Name, u.Email, u.Mobile, u.Designation, u.Department, u.Institution,
                               u.SetterType, u.ExperienceYears, u.AreaOfInterest, u.RegistrationStatus, u.Source })
            .ToListAsync());
}