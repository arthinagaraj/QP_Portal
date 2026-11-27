using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QpPortal.Api.Auth;
using QpPortal.Api.Data;
using QpPortal.Api.Services;

namespace QpPortal.Api.Controllers;

// Setter: "Suggest questions" for a unit while setting the QP
[ApiController]
[Authorize(Policy = Perms.PaperEdit)]
public class SuggestController(AppDb db, QuestionSuggester suggester) : ControllerBase
{
    public record SuggestDto(int UnitNo, decimal Marks, int Count = 5);

    int Me => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("api/setter/assignments/{id:int}/suggest")]
    public async Task<IActionResult> Suggest(int id, SuggestDto dto)
    {
        var a = await db.Assignments.Include(x => x.Course).ThenInclude(c => c.Units).FirstOrDefaultAsync(x => x.Id == id);
        if (a is null || a.SetterId != Me) return NotFound();
        if (a.Status == "Submitted") return BadRequest("Paper already submitted.");
        var u = a.Course.Units.FirstOrDefault(x => x.UnitNo == dto.UnitNo);
        if (u is null) return BadRequest("Unknown unit.");

        var avoid = await db.Questions.Where(q => q.AssignmentId == id).Select(q => q.PlainText).ToListAsync();
        var r = await suggester.SuggestAsync($"{a.Course.Code} {a.Course.Title}", u.UnitNo, u.Title, u.Topics ?? "", u.CoText ?? "",
                                             dto.Marks, Math.Clamp(dto.Count, 1, 8), avoid.Where(x => x != null).Select(x => x!).ToList());
        return Ok(r);
    }
}
