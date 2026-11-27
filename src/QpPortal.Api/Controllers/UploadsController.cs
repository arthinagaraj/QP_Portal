using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QpPortal.Api.Auth;
using QpPortal.Api.Data;

namespace QpPortal.Api.Controllers;

[ApiController]
[Route("api/uploads")]
public class UploadsController(AppDb db) : ControllerBase
{
    static IXLWorksheet Sheet(XLWorkbook wb, string name) =>
        wb.Worksheets.FirstOrDefault(w => w.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? wb.Worksheet(1);

    static Dictionary<string, int> Headers(IXLWorksheet ws) =>
        ws.Row(1).CellsUsed().ToDictionary(c => c.GetString().Trim(), c => c.Address.ColumnNumber,
                                           StringComparer.OrdinalIgnoreCase);

    static string Text(IXLRow row, Dictionary<string, int> h, string col) =>
        h.TryGetValue(col, out var i) ? row.Cell(i).GetFormattedString().Trim() : "";

    static DateOnly? Dob(IXLRow row, Dictionary<string, int> h)
    {
        if (!h.TryGetValue("DateOfBirth", out var i)) return null;
        var c = row.Cell(i);
        if (c.DataType == XLDataType.DateTime) return DateOnly.FromDateTime(c.GetDateTime());
        return DateOnly.TryParseExact(c.GetFormattedString().Trim(), ["yyyy-MM-dd", "dd-MM-yyyy", "dd/MM/yyyy"],
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    }

    static string? MissingHeaders(Dictionary<string, int> h, params string[] required)
    {
        var missing = required.Where(r => !h.ContainsKey(r)).ToArray();
        return missing.Length == 0 ? null : "Missing columns: " + string.Join(", ", missing);
    }

    // 1. QP setter database
    [HttpPost("setters")]
    [Authorize(Policy = Perms.SetterUpload)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Setters(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        using var wb = new XLWorkbook(stream);
        var ws = Sheet(wb, "Setters");
        var h = Headers(ws);
        var missing = MissingHeaders(h, "Name", "Email", "Mobile", "DateOfBirth", "SetterType");
        if (missing is not null) return BadRequest(missing);

        var setterRoleId = (await db.Roles.SingleAsync(r => r.Name == "Setter")).Id;
        var users = await db.Users.Include(u => u.UserRoles).ToListAsync();
        var byEmail = users.ToDictionary(u => u.Email.ToLowerInvariant());

        int added = 0, updated = 0;
        var errors = new List<string>();

        foreach (var row in ws.RowsUsed().Skip(1))
        {
            var r = row.RowNumber();
            var name = Text(row, h, "Name");
            var email = Text(row, h, "Email");
            var mobile = Text(row, h, "Mobile");
            var type = Text(row, h, "SetterType");
            var dob = Dob(row, h);

            if (name == "" || email == "") { errors.Add($"Row {r}: Name and Email are required."); continue; }
            if (!Regex.IsMatch(mobile, @"^\d{10}$")) { errors.Add($"Row {r}: Mobile must be 10 digits."); continue; }
            if (dob is null) { errors.Add($"Row {r}: DateOfBirth must be YYYY-MM-DD."); continue; }
            if (type != "Internal" && type != "External") { errors.Add($"Row {r}: SetterType must be Internal or External."); continue; }

            byEmail.TryGetValue(email.ToLowerInvariant(), out var u);
            if (users.Any(o => o != u && o.Mobile == mobile)) { errors.Add($"Row {r}: Mobile {mobile} already used by another person."); continue; }

            if (u is null)
            {
                u = new User { Email = email, Source = "Upload" };
                u.UserRoles.Add(new UserRole { RoleId = setterRoleId });
                db.Users.Add(u);
                users.Add(u);
                byEmail[email.ToLowerInvariant()] = u;
                added++;
            }
            else
            {
                if (!u.UserRoles.Any(x => x.RoleId == setterRoleId))
                    u.UserRoles.Add(new UserRole { RoleId = setterRoleId });
                updated++;
            }

            u.Name = name; u.Mobile = mobile; u.DateOfBirth = dob; u.SetterType = type;
            u.Designation = Text(row, h, "Designation");
            u.Department = Text(row, h, "Department");
            u.Institution = Text(row, h, "Institution");
            u.Qualification = Text(row, h, "Qualification");
            u.AreaOfInterest = Text(row, h, "AreaOfInterest");
            u.ExperienceYears = int.TryParse(Text(row, h, "ExperienceYears"), out var y) ? y : null;
            u.RegistrationStatus = "Approved";
        }

        await db.SaveChangesAsync();
        return Ok(new { added, updated, errors });
    }

    // 2. Courses + regulation + syllabus (one row per unit)
    [HttpPost("courses")]
    [Authorize(Policy = Perms.CourseUpload)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Courses(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        using var wb = new XLWorkbook(stream);
        var ws = Sheet(wb, "Courses");
        var h = Headers(ws);
        var missing = MissingHeaders(h, "Regulation", "CourseCode", "CourseTitle", "UnitNo", "UnitTitle", "CO", "Topics");
        if (missing is not null) return BadRequest(missing);

        var errors = new List<string>();
        var rows = new List<(string Reg, string Code, string Title, int UnitNo, string UnitTitle, string Co, string Topics)>();
        foreach (var row in ws.RowsUsed().Skip(1))
        {
            var r = row.RowNumber();
            var reg = Text(row, h, "Regulation"); var code = Text(row, h, "CourseCode");
            if (reg == "" || code == "") { errors.Add($"Row {r}: Regulation and CourseCode are required."); continue; }
            if (!int.TryParse(Text(row, h, "UnitNo"), out var unitNo) || unitNo < 1 || unitNo > 10)
            { errors.Add($"Row {r}: UnitNo must be a number 1-10."); continue; }
            var topics = Text(row, h, "Topics");
            if (topics == "") { errors.Add($"Row {r}: Topics are required (the AI agent uses them)."); continue; }
            rows.Add((reg, code, Text(row, h, "CourseTitle"), unitNo, Text(row, h, "UnitTitle"), Text(row, h, "CO"), topics));
        }

        var regs = await db.Regulations.ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase);
        var courses = await db.Courses.Include(c => c.Units).ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase);
        int coursesAdded = 0, coursesUpdated = 0, units = 0;

        foreach (var g in rows.GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase))
        {
            var first = g.First();
            if (!regs.TryGetValue(first.Reg, out var reg))
            {
                reg = new Regulation { Code = first.Reg, Name = "Regulation " + first.Reg };
                db.Regulations.Add(reg);
                regs[first.Reg] = reg;
            }

            if (!courses.TryGetValue(first.Code, out var course))
            {
                course = new Course { Code = first.Code };
                db.Courses.Add(course);
                courses[first.Code] = course;
                coursesAdded++;
            }
            else coursesUpdated++;

            course.Title = first.Title;
            course.Regulation = reg;

            foreach (var x in g)
            {
                var unit = course.Units.FirstOrDefault(u => u.UnitNo == x.UnitNo);
                if (unit is null) { unit = new SyllabusUnit { UnitNo = x.UnitNo }; course.Units.Add(unit); }
                unit.Title = x.UnitTitle; unit.CoText = x.Co; unit.Topics = x.Topics;
                units++;
            }
        }

        await db.SaveChangesAsync();
        return Ok(new { coursesAdded, coursesUpdated, units, errors });
    }
}