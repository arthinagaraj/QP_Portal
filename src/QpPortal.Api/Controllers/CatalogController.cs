using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QpPortal.Api.Auth;
using QpPortal.Api.Data;

namespace QpPortal.Api.Controllers;

// CoE / DCoE: add or remove regulations, remove a course (list endpoints stay in MastersController)
[ApiController]
[Authorize(Policy = Perms.CourseUpload)]
public class CatalogController(AppDb db) : ControllerBase
{
    public record RegulationDto(string Code);

    [HttpPost("api/regulations")]
    public async Task<IActionResult> AddRegulation(RegulationDto dto)
    {
        var code = (dto.Code ?? "").Trim().ToUpperInvariant();
        if (!Regex.IsMatch(code, "^[A-Z0-9-]{2,20}$"))
            return BadRequest("Regulation code: 2–20 letters/digits, e.g. R2025.");
        if (await db.Regulations.AnyAsync(r => r.Code == code))
            return BadRequest($"Regulation {code} already exists.");
        var reg = new Regulation { Code = code };
        db.Regulations.Add(reg);
        await db.SaveChangesAsync();
        return Ok(new { reg.Id, reg.Code });
    }

    [HttpDelete("api/regulations/{id:int}")]
    public async Task<IActionResult> DeleteRegulation(int id)
    {
        var reg = await db.Regulations.FindAsync(id);
        if (reg is null) return NotFound();
        if (await db.Courses.AnyAsync(c => c.RegulationId == id) || await db.QpTemplates.AnyAsync(t => t.RegulationId == id))
            return BadRequest($"{reg.Code} has courses or QP frameworks – remove those first.");
        db.Regulations.Remove(reg);
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("api/courses/{id:int}")]
    public async Task<IActionResult> DeleteCourse(int id)
    {
        var c = await db.Courses.FindAsync(id);
        if (c is null) return NotFound();
        if (await db.Assignments.AnyAsync(a => a.CourseId == id))
            return BadRequest($"{c.Code} is already mapped to a setter – it cannot be removed.");
        db.Courses.Remove(c);   // unit syllabus rows are removed by ON DELETE CASCADE
        await db.SaveChangesAsync();
        return Ok();
    }
}
