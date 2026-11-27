using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QpPortal.Api.Auth;
using QpPortal.Api.Data;

namespace QpPortal.Api.Controllers;

[ApiController]
[Route("api/templates")]
[Authorize(Policy = Perms.TemplateManage)]
public class TemplatesController(AppDb db) : ControllerBase
{
    public const int UnitsPerCourse = 5;

    public record PartDto(string PartName, string Title, string Distribution, int QuestionCount,
                          decimal MarksEach, bool IsEitherOr);
    public record TemplateDto(int RegulationId, string ExamType, string Name, string Duration,
                              decimal MaxMarks, string? Instructions, List<PartDto> Parts);

    public static decimal Total(IEnumerable<QpTemplatePart> parts) =>
        parts.Sum(p => (p.Distribution == "PerUnit" ? p.QuestionCount * UnitsPerCourse : p.QuestionCount) * p.MarksEach);

    static object Shape(QpTemplate t) => new
    {
        t.Id, t.RegulationId, Regulation = t.Regulation?.Code, t.ExamType, t.Name, t.Duration, t.MaxMarks,
        t.Instructions, t.Version, t.Status, Total = Total(t.Parts),
        Parts = t.Parts.OrderBy(p => p.DisplayOrder).Select(p => new
        {
            p.PartName, p.Title, p.Distribution, p.QuestionCount, p.MarksEach, p.IsEitherOr,
            TotalQuestions = p.Distribution == "PerUnit" ? p.QuestionCount * UnitsPerCourse : p.QuestionCount,
            PartMarks = (p.Distribution == "PerUnit" ? p.QuestionCount * UnitsPerCourse : p.QuestionCount) * p.MarksEach
        })
    };

    static string? Validate(TemplateDto dto)
    {
        if (dto.Parts is null || dto.Parts.Count == 0) return "Add at least one part.";
        foreach (var p in dto.Parts)
        {
            if (p.Distribution != "PerUnit" && p.Distribution != "AnyUnit") return $"Part {p.PartName}: Distribution must be PerUnit or AnyUnit.";
            if (p.QuestionCount <= 0 || p.MarksEach <= 0) return $"Part {p.PartName}: count and marks must be positive.";
        }
        return null;
    }

    [HttpGet]
    public async Task<IActionResult> List(int? regulationId) =>
        Ok((await db.QpTemplates.Include(t => t.Regulation).Include(t => t.Parts)
                .Where(t => regulationId == null || t.RegulationId == regulationId)
                .OrderBy(t => t.RegulationId).ThenByDescending(t => t.Version).ToListAsync())
            .Select(Shape));

    [HttpPost]
    public async Task<IActionResult> Create(TemplateDto dto)
    {
        if (Validate(dto) is { } err) return BadRequest(err);
        if (!await db.Regulations.AnyAsync(r => r.Id == dto.RegulationId)) return BadRequest("Unknown regulation.");
        var version = 1 + (await db.QpTemplates.Where(t => t.RegulationId == dto.RegulationId && t.ExamType == dto.ExamType)
                                               .MaxAsync(t => (int?)t.Version) ?? 0);
        var t = new QpTemplate
        {
            RegulationId = dto.RegulationId, ExamType = dto.ExamType, Name = dto.Name, Duration = dto.Duration,
            MaxMarks = dto.MaxMarks, Instructions = dto.Instructions, Version = version,
            Parts = dto.Parts.Select((p, i) => new QpTemplatePart
            {
                PartName = p.PartName, Title = p.Title, Distribution = p.Distribution, QuestionCount = p.QuestionCount,
                MarksEach = p.MarksEach, IsEitherOr = p.IsEitherOr, DisplayOrder = i + 1
            }).ToList()
        };
        db.QpTemplates.Add(t);
        await db.SaveChangesAsync();
        await db.Entry(t).Reference(x => x.Regulation).LoadAsync();
        return Ok(Shape(t));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, TemplateDto dto)
    {
        if (Validate(dto) is { } err) return BadRequest(err);
        var t = await db.QpTemplates.Include(x => x.Parts).Include(x => x.Regulation).FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        if (t.Status != "Draft") return BadRequest("Only a Draft template can be edited. Create a new version instead.");
        t.Name = dto.Name; t.Duration = dto.Duration; t.MaxMarks = dto.MaxMarks; t.Instructions = dto.Instructions;
        db.QpTemplateParts.RemoveRange(t.Parts);
        t.Parts = dto.Parts.Select((p, i) => new QpTemplatePart
        {
            PartName = p.PartName, Title = p.Title, Distribution = p.Distribution, QuestionCount = p.QuestionCount,
            MarksEach = p.MarksEach, IsEitherOr = p.IsEitherOr, DisplayOrder = i + 1
        }).ToList();
        await db.SaveChangesAsync();
        return Ok(Shape(t));
    }

    [HttpPost("{id:int}/publish")]
    public async Task<IActionResult> Publish(int id)
    {
        var t = await db.QpTemplates.Include(x => x.Parts).Include(x => x.Regulation).FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        if (t.Status != "Draft") return BadRequest($"Template is already {t.Status}.");
        var total = Total(t.Parts);
        if (total != t.MaxMarks) return BadRequest($"Parts add up to {total} marks but maximum marks is {t.MaxMarks}.");

        var old = await db.QpTemplates.Where(x => x.RegulationId == t.RegulationId && x.ExamType == t.ExamType
                                                && x.Status == "Published").ToListAsync();
        old.ForEach(x => x.Status = "Retired");
        t.Status = "Published";
        await db.SaveChangesAsync();
        return Ok(Shape(t));
    }
}