using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QpPortal.Api.Auth;
using QpPortal.Api.Data;

namespace QpPortal.Api.Controllers;

[ApiController]
[Route("api/suggestions")]
[Authorize(Policy = Perms.SetterAssign)]
public class SuggestionsController(AppDb db) : ControllerBase
{
    static readonly HashSet<string> Stop = ["and", "the", "for", "with", "its", "into", "from", "using", "analysis", "introduction"];

    static HashSet<string> Words(string? s) =>
        (s ?? "").ToLowerInvariant()
            .Split([',', ' ', ';', '-', '/', '(', ')', '.', ':'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2 && !Stop.Contains(w))
            .ToHashSet();

    static int DesignationScore(string? d)
    {
        d = (d ?? "").ToLowerInvariant();
        if (d.Contains("assistant")) return 10;
        if (d.Contains("associate")) return 15;
        if (d.Contains("professor")) return 20;
        return 5;
    }

    // Score out of 100: interest match 60 + designation 20 + experience 20, minus 5 per active assignment
    [HttpGet]
    public async Task<IActionResult> Get(int courseId, string? setterType = null)
    {
        var course = await db.Courses.Include(c => c.Units).FirstOrDefaultAsync(c => c.Id == courseId);
        if (course is null) return NotFound();
        var courseWords = Words(course.Title + "," + string.Join(",", course.Units.Select(u => u.Title + "," + u.Topics)));

        var setters = await db.Users
            .Where(u => u.IsActive && u.RegistrationStatus == "Approved" && u.UserRoles.Any(r => r.Role.Name == "Setter"))
            .Where(u => setterType == null || u.SetterType == setterType)
            .ToListAsync();

        var load = await db.Assignments.Where(a => a.Status == "Assigned" || a.Status == "InProgress")
                                       .GroupBy(a => a.SetterId)
                                       .Select(g => new { g.Key, Count = g.Count() })
                                       .ToDictionaryAsync(x => x.Key, x => x.Count);

        var ranked = setters.Select(u =>
        {
            var interest = Words(u.AreaOfInterest);
            var matched = interest.Where(courseWords.Contains).ToList();
            var interestScore = interest.Count == 0 ? 0 : 60.0 * matched.Count / interest.Count;
            var expScore = Math.Min(u.ExperienceYears ?? 0, 20);
            var active = load.GetValueOrDefault(u.Id);
            var score = Math.Max(0, Math.Round(interestScore + DesignationScore(u.Designation) + expScore - 5 * active));
            return new
            {
                SetterId = u.Id, u.Name, u.Designation, u.Department, u.Institution, u.SetterType,
                u.ExperienceYears, u.AreaOfInterest, MatchedTopics = matched, ActiveAssignments = active, Score = score
            };
        })
        .OrderByDescending(x => x.Score).Take(10).ToList();

        return Ok(ranked);
    }
}