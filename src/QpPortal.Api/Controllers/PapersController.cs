using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QpPortal.Api.Auth;
using QpPortal.Api.Data;
using QpPortal.Api.Services;

namespace QpPortal.Api.Controllers;

// Exam cell (CoE / DCoE) access to submitted question papers: view, Word (.docx, editable), PDF (print view)
[ApiController]
[Route("api/papers")]
[Authorize(Policy = Perms.PaperDownload)]
public class PapersController(AppDb db, IWebHostEnvironment env) : ControllerBase
{
    public const string College = "KARPAGAM COLLEGE OF ENGINEERING, COIMBATORE";

    public static async Task<(Assignment A, List<QpPart> Parts)?> Load(AppDb db, int id)
    {
        var a = await db.Assignments
            .Include(x => x.Course).ThenInclude(c => c.Units)
            .Include(x => x.Template).ThenInclude(t => t.Parts)
            .Include(x => x.Template).ThenInclude(t => t.Regulation)
            .Include(x => x.Setter)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return null;

        var qs = await db.Questions.Where(q => q.AssignmentId == id).ToListAsync();
        var slots = SetterController.Slots(a.Template, a.Course.Units.Count);

        var parts = a.Template.Parts.OrderBy(p => p.DisplayOrder).Select(p =>
        {
            var ps = slots.Where(s => s.PartName == p.PartName).ToList();
            var n = ps.Select(s => s.QNo).Distinct().Count();
            var heading = $"PART {p.PartName} – {p.Title} ({n} × {p.MarksEach:0.#} = {n * p.MarksEach:0.#} Marks)"
                          + (p.IsEitherOr ? " – answer either (a) or (b)" : "");
            var rows = ps.Select(s =>
            {
                var mine = qs.Where(q => q.PartName == s.PartName && q.QNo == s.QNo && q.OptionLetter == s.Option).ToList();
                var subs = mine.Where(q => q.SubLabel != "").OrderBy(q => Array.IndexOf(SetterController.SubLabels, q.SubLabel)).ToList();
                var use = subs.Count > 0 ? subs : mine.Where(q => q.SubLabel == "").ToList();
                var qLabel = s.Option == "" ? $"{s.QNo}" : $"{s.QNo} ({s.Option})";
                return new QpRow(qLabel, s.Option, use.Select(q => new QpEntry(q.SubLabel, q.BodyHtml, q.Marks, $"CO{q.UnitNo}", q.Btl)).ToList());
            }).ToList();
            return new QpPart(heading, rows);
        }).ToList();

        return (a, parts);
    }

    // JSON for the on-screen paper (also used by the scrutiny page)
    public static object PaperView(Assignment a, List<QpPart> parts, bool showSetter = true) => new
        {
            a.Id, College, ExamLine = $"{a.Template.ExamType} Examinations – {a.ExamSession}",
            Course = new { a.Course.Code, a.Course.Title }, Regulation = a.Template.Regulation!.Code,
            a.Template.Duration, a.Template.MaxMarks, a.Template.Instructions,
            Setter = showSetter ? a.Setter.Name : "(hidden for scrutiny)", SetterType = showSetter ? a.Setter.SetterType : "", a.SubmittedAt, Parts = parts
        };

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var r = await Load(db, id);
        if (r is null) return NotFound();
        if (r.Value.A.Status != "Submitted") return BadRequest("The question paper has not been submitted by the setter yet.");
        return Ok(PaperView(r.Value.A, r.Value.Parts));
    }

    [HttpGet("{id:int}/docx")]
    public async Task<IActionResult> Docx(int id)
    {
        var r = await Load(db, id);
        if (r is null) return NotFound();
        var (a, parts) = r.Value;
        if (a.Status != "Submitted") return BadRequest("The question paper has not been submitted by the setter yet.");

        var logoPath = Path.Combine(env.ContentRootPath, "Assets", "kce-logo.png");
        var logo = System.IO.File.Exists(logoPath) ? await System.IO.File.ReadAllBytesAsync(logoPath) : null;
        var doc = new QpDoc(College, $"{a.Template.ExamType} Examinations – {a.ExamSession}",
            $"{a.Course.Code} – {a.Course.Title}", $"(Regulation {a.Template.Regulation!.Code})",
            a.Template.Duration, a.Template.MaxMarks, a.Template.Instructions, parts, logo);

        var fileName = $"QP_{a.Course.Code}_{a.ExamSession.Replace('/', '-').Replace(' ', '_')}.docx";
        return File(QpDocx.Build(doc), "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
    }
}
