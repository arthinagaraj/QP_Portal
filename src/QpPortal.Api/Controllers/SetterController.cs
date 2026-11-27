using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QpPortal.Api.Auth;
using QpPortal.Api.Data;
using QpPortal.Api.Services;

namespace QpPortal.Api.Controllers;

public record Slot(string PartName, string PartTitle, int QNo, string Option, decimal Marks, int? UnitNo,
                   string Distribution, bool EitherOr);

[ApiController]
[Route("api/setter")]
[Authorize(Policy = Perms.PaperEdit)]
public class SetterController(AppDb db, EmailService email) : ControllerBase
{
    public record ProfileDto(string Name, string Mobile, string? Designation, string? Department, string? Institution,
        string Address, string AccountHolder, string AccountNumber, string ConfirmAccountNumber,
        string BankName, string? Branch, string Ifsc, string Pan);
    public record SaveQuestionDto(string PartName, int QNo, string? Option, int UnitNo,
        string BodyHtml, string PlainText, string? Btl);

    int Me => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Question slots from the framework: PerUnit parts numbered unit by unit; AnyUnit parts at the end
    public static List<Slot> Slots(QpTemplate t, int unitCount)
    {
        var list = new List<Slot>();
        var q = 0;
        foreach (var p in t.Parts.OrderBy(p => p.DisplayOrder))
        {
            string[] opts = p.IsEitherOr ? ["a", "b"] : [""];
            if (p.Distribution == "PerUnit")
            {
                for (var u = 1; u <= unitCount; u++)
                    for (var k = 0; k < p.QuestionCount; k++)
                    {
                        q++;
                        foreach (var o in opts) list.Add(new Slot(p.PartName, p.Title, q, o, p.MarksEach, u, p.Distribution, p.IsEitherOr));
                    }
            }
            else
            {
                for (var k = 0; k < p.QuestionCount; k++)
                {
                    q++;
                    foreach (var o in opts) list.Add(new Slot(p.PartName, p.Title, q, o, p.MarksEach, null, p.Distribution, p.IsEitherOr));
                }
            }
        }
        return list;
    }

    async Task<Assignment?> Mine(int id) =>
        await db.Assignments.Include(a => a.Course).ThenInclude(c => c.Units)
                            .Include(a => a.Template).ThenInclude(t => t.Parts)
                            .FirstOrDefaultAsync(a => a.Id == id && a.SetterId == Me && a.Status != "Cancelled");

    // ---------- Profile + bank ----------
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var u = await db.Users.FindAsync(Me);
        var b = await db.BankDetails.FindAsync(Me);
        return Ok(new
        {
            u!.Name, u.Email, u.Mobile, u.Designation, u.Department, u.Institution, u.SetterType,
            Address = b?.Address, AccountHolder = b?.AccountHolder, AccountNumber = b?.AccountNumber,
            BankName = b?.BankName, Branch = b?.Branch, Ifsc = b?.Ifsc, Pan = b?.Pan, Complete = b is not null
        });
    }

    [HttpPut("profile")]
    public async Task<IActionResult> SaveProfile(ProfileDto d)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(d.Name)) errors.Add("Name is required.");
        if (!Regex.IsMatch(d.Mobile ?? "", @"^\d{10}$")) errors.Add("Mobile must be 10 digits.");
        if (string.IsNullOrWhiteSpace(d.Address)) errors.Add("Address is required.");
        if (string.IsNullOrWhiteSpace(d.AccountHolder) || string.IsNullOrWhiteSpace(d.BankName)) errors.Add("Account holder and bank name are required.");
        if (!Regex.IsMatch(d.AccountNumber ?? "", @"^\d{9,18}$")) errors.Add("Account number must be 9-18 digits.");
        if (d.AccountNumber != d.ConfirmAccountNumber) errors.Add("Account numbers do not match.");
        if (!Regex.IsMatch((d.Ifsc ?? "").ToUpperInvariant(), @"^[A-Z]{4}0[A-Z0-9]{6}$")) errors.Add("IFSC is invalid (e.g. SBIN0001234).");
        if (!Regex.IsMatch((d.Pan ?? "").ToUpperInvariant(), @"^[A-Z]{5}\d{4}[A-Z]$")) errors.Add("PAN is invalid (e.g. ABCDE1234F).");
        if (errors.Count > 0) return BadRequest(new { errors });

        var u = await db.Users.FindAsync(Me);
        u!.Name = d.Name.Trim(); u.Mobile = d.Mobile; u.Designation = d.Designation;
        u.Department = d.Department; u.Institution = d.Institution;

        var b = await db.BankDetails.FindAsync(Me);
        if (b is null) { b = new BankDetail { UserId = Me }; db.BankDetails.Add(b); }
        b.Address = d.Address.Trim(); b.AccountHolder = d.AccountHolder.Trim(); b.AccountNumber = d.AccountNumber;
        b.BankName = d.BankName.Trim(); b.Branch = d.Branch; b.Ifsc = d.Ifsc.ToUpperInvariant();
        b.Pan = d.Pan.ToUpperInvariant(); b.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { saved = true });
    }

    // ---------- Assignments ----------
    [HttpGet("assignments")]
    public async Task<IActionResult> MyAssignments() =>
        Ok(await db.Assignments.Where(a => a.SetterId == Me && a.Status != "Cancelled")
            .Select(a => new { a.Id, a.Course.Code, a.Course.Title, a.ExamSession, a.DueDate, a.Status })
            .ToListAsync());

    [HttpGet("assignments/by-token/{token}")]
    public async Task<IActionResult> ByToken(string token)
    {
        var a = await db.Assignments.FirstOrDefaultAsync(x => x.LinkToken == token);
        if (a is null) return NotFound("Invalid link.");
        if (a.SetterId != Me) return Forbid();
        return Ok(new { a.Id });
    }

    // Everything the split-screen page needs: framework, units (syllabus), slots with saved questions
    [HttpGet("assignments/{id:int}/structure")]
    public async Task<IActionResult> Structure(int id)
    {
        var a = await Mine(id);
        if (a is null) return NotFound();
        var saved = await db.Questions.Where(q => q.AssignmentId == id).ToListAsync();
        var units = a.Course.Units.OrderBy(u => u.UnitNo).ToList();

        var slots = Slots(a.Template, units.Count).Select(s =>
        {
            var q = saved.FirstOrDefault(x => x.PartName == s.PartName && x.QNo == s.QNo && x.OptionLetter == s.Option);
            return new
            {
                s.PartName, s.PartTitle, s.QNo, s.Option, s.Marks, s.UnitNo, s.Distribution, s.EitherOr,
                Question = q is null ? null : new
                {
                    q.UnitNo, q.BodyHtml, q.PlainText, q.Btl, q.BtlSource, q.MatchScore, q.SyllabusVerdict, q.Clarity,
                    Notes = (q.AiNotes ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries)
                }
            };
        });

        return Ok(new
        {
            a.Id, a.Status, a.ExamSession, a.DueDate,
            Course = new { a.Course.Code, a.Course.Title },
            Template = new { a.Template.Name, a.Template.Duration, a.Template.MaxMarks, a.Template.Instructions },
            Units = units.Select(u => new { u.UnitNo, u.Title, Co = u.CoText, u.Topics }),
            ProfileComplete = await db.BankDetails.AnyAsync(b => b.UserId == Me),
            Slots = slots
        });
    }

    // Save one question -> AI agent checks it immediately
    [HttpPut("assignments/{id:int}/questions")]
    public async Task<IActionResult> SaveQuestion(int id, SaveQuestionDto d)
    {
        var a = await Mine(id);
        if (a is null) return NotFound();
        if (a.Status == "Submitted") return BadRequest("This question paper is already submitted and locked.");

        var units = a.Course.Units.OrderBy(u => u.UnitNo).ToList();
        var opt = d.Option ?? "";
        var slot = Slots(a.Template, units.Count).FirstOrDefault(s => s.PartName == d.PartName && s.QNo == d.QNo && s.Option == opt);
        if (slot is null) return BadRequest("This question number is not in the framework.");
        var unitNo = slot.UnitNo ?? d.UnitNo;
        if (!units.Any(u => u.UnitNo == unitNo)) return BadRequest("Choose a valid unit.");
        if (string.IsNullOrWhiteSpace(d.PlainText) && !Regex.IsMatch(d.BodyHtml ?? "", "<img", RegexOptions.IgnoreCase))
            return BadRequest("Question text is empty.");

        var info = units.Select(u => new UnitInfo(u.UnitNo, u.Title, u.Topics)).ToList();
        var r = QuestionAnalyzer.Analyze(d.PlainText ?? "", d.BodyHtml ?? "", slot.Marks, unitNo, info);

        var q = await db.Questions.FirstOrDefaultAsync(x => x.AssignmentId == id && x.PartName == slot.PartName
                                                         && x.QNo == slot.QNo && x.OptionLetter == opt);
        if (q is null)
        {
            q = new Question { AssignmentId = id, PartName = slot.PartName, QNo = slot.QNo, OptionLetter = opt };
            db.Questions.Add(q);
        }
        q.UnitNo = unitNo; q.BodyHtml = d.BodyHtml ?? ""; q.PlainText = d.PlainText ?? ""; q.Marks = slot.Marks;
        q.Btl = string.IsNullOrEmpty(d.Btl) ? r.Btl : d.Btl;
        q.BtlSource = string.IsNullOrEmpty(d.Btl) || d.Btl == r.Btl ? "AI" : "Setter";
        q.MatchScore = r.MatchScore; q.SyllabusVerdict = r.SyllabusVerdict; q.Clarity = r.Clarity;
        q.AiNotes = string.Join("|", r.Notes); q.UpdatedAt = DateTime.UtcNow;
        if (a.Status == "Assigned") a.Status = "InProgress";
        await db.SaveChangesAsync();

        return Ok(new { q.Btl, q.BtlSource, AiBtl = r.Btl, r.Verb, r.MatchScore, r.SyllabusVerdict, r.BetterUnit,
                        r.Clarity, r.Notes, r.Matched, r.NotInSyllabus });
    }

    // Final submit: everything filled, in syllabus and complete -> claim calculated -> locked
    [HttpPost("assignments/{id:int}/submit")]
    public async Task<IActionResult> Submit(int id)
    {
        var a = await Mine(id);
        if (a is null) return NotFound();
        if (a.Status == "Submitted") return BadRequest("Already submitted.");

        var problems = new List<string>();
        if (!await db.BankDetails.AnyAsync(b => b.UserId == Me)) problems.Add("Personal and bank details are not filled.");

        var saved = await db.Questions.Where(q => q.AssignmentId == id).ToListAsync();
        foreach (var s in Slots(a.Template, a.Course.Units.Count))
        {
            var label = $"Part {s.PartName} Q{s.QNo}{(s.Option == "" ? "" : "(" + s.Option + ")")}";
            var q = saved.FirstOrDefault(x => x.PartName == s.PartName && x.QNo == s.QNo && x.OptionLetter == s.Option);
            if (q is null) problems.Add($"{label}: not entered.");
            else if (q.SyllabusVerdict != "In") problems.Add($"{label}: deviates more than 50% from the unit syllabus.");
            else if (q.Clarity != "Complete") problems.Add($"{label}: incomplete - {q.AiNotes?.Replace("|", "; ")}");
        }
        if (problems.Count > 0) return BadRequest(new { problems });

        var setter = await db.Users.FindAsync(Me);
        var rate = await db.ClaimRates.FindAsync(setter!.SetterType ?? "Internal")
                   ?? new ClaimRate { SetterType = "Internal", QpFee = 1000, TdsPercent = 10 };
        var gross = rate.QpFee + rate.TravelAllowance;
        var tds = Math.Round(rate.QpFee * rate.TdsPercent / 100, 0);
        db.Claims.Add(new QpClaim
        {
            AssignmentId = id, SetterType = rate.SetterType, QpFee = rate.QpFee, TravelAllowance = rate.TravelAllowance,
            Gross = gross, TdsPercent = rate.TdsPercent, Tds = tds, Net = gross - tds
        });
        a.Status = "Submitted";
        a.SubmittedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        try
        {
            await email.SendAsync(setter.Email, setter.Name, $"QP received: {a.Course.Code} {a.Course.Title}",
                $"<p>Dear {setter.Name},</p><p>Your question paper for <b>{a.Course.Code} - {a.Course.Title}</b> " +
                $"({a.ExamSession}) has been received. Net claim: Rs. {gross - tds:N0}.</p><p>Controller of Examinations</p>");
        }
        catch { /* e-mail failure must not block submission */ }

        return Ok(new { submitted = true, net = gross - tds });
    }

    // Claim for printing - never includes the questions
    [HttpGet("assignments/{id:int}/claim")]
    public async Task<IActionResult> GetClaim(int id)
    {
        var a = await Mine(id);
        if (a is null) return NotFound();
        var c = await db.Claims.FindAsync(id);
        if (c is null) return BadRequest("Claim is available after the question paper is submitted.");
        var u = await db.Users.FindAsync(Me);
        var b = await db.BankDetails.FindAsync(Me);
        return Ok(new
        {
            Course = $"{a.Course.Code} - {a.Course.Title}", a.ExamSession, a.SubmittedAt,
            Setter = new { u!.Name, u.Designation, u.Department, u.Institution, u.Email, u.Mobile, u.SetterType },
            Bank = new { b!.AccountHolder, b.AccountNumber, b.BankName, b.Branch, b.Ifsc, b.Pan, b.Address },
            Claim = new { c.QpFee, c.TravelAllowance, c.Gross, c.TdsPercent, c.Tds, c.Net }
        });
    }

    [HttpGet("assignments/{id:int}/claim.pdf")]
    public async Task<IActionResult> ClaimPdfDownload(int id)
    {
        var a = await Mine(id);
        if (a is null) return NotFound();
        var c = await db.Claims.FindAsync(id);
        if (c is null) return BadRequest("Claim is available after the question paper is submitted.");
        var u = await db.Users.FindAsync(Me);
        var b = await db.BankDetails.FindAsync(Me);
        var pdf = ClaimPdf.Build($"{a.Course.Code} - {a.Course.Title}", a.ExamSession, a.SubmittedAt, u!, b!, c);
        return File(pdf, "application/pdf", $"Claim_{a.Course.Code}.pdf");
    }
}