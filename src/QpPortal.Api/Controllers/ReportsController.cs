using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QpPortal.Api.Auth;
using QpPortal.Api.Data;

namespace QpPortal.Api.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController(AppDb db) : ControllerBase
{
    // Analysis page: QP / claim received vs not received
    [HttpGet("status")]
    [Authorize(Policy = Perms.ReportView)]
    public async Task<IActionResult> Status()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var claims = await db.Claims.ToDictionaryAsync(c => c.AssignmentId);

        var rows = (await db.Assignments.Where(a => a.Status != "Cancelled")
            .Select(a => new
            {
                a.Id, a.Course.Code, a.Course.Title, Regulation = a.Course.Regulation.Code,
                Setter = a.Setter.Name, a.Setter.SetterType, a.Setter.Department, a.Setter.Mobile,
                a.ExamSession, a.DueDate, a.Status, a.InviteSentAt, a.SubmittedAt
            }).ToListAsync())
            .Select(a => new
            {
                a.Id, Course = a.Code + " - " + a.Title, a.Regulation, a.Setter, a.SetterType, a.Department, a.Mobile,
                a.ExamSession, a.DueDate, a.Status, InviteSent = a.InviteSentAt != null,
                QpReceived = a.Status == "Submitted",
                ClaimReceived = claims.ContainsKey(a.Id),
                Net = claims.TryGetValue(a.Id, out var c) ? c.Net : 0,
                Overdue = a.Status != "Submitted" && a.DueDate < today
            }).ToList();

        var mappedCourseIds = await db.Assignments.Where(a => a.Status != "Cancelled").Select(a => a.CourseId).ToListAsync();
        var notMapped = await db.Courses.Where(c => !mappedCourseIds.Contains(c.Id))
                                        .Select(c => c.Code + " - " + c.Title).ToListAsync();

        return Ok(new
        {
            Summary = new
            {
                Subjects = await db.Courses.CountAsync(),
                Mapped = rows.Count,
                NotMapped = notMapped.Count,
                QpReceived = rows.Count(r => r.QpReceived),
                QpNotReceived = rows.Count(r => !r.QpReceived),
                Overdue = rows.Count(r => r.Overdue),
                ClaimsReceived = rows.Count(r => r.ClaimReceived),
                TotalNetPayable = rows.Sum(r => r.Net)
            },
            Rows = rows,
            NotMappedSubjects = notMapped
        });
    }

    // Final Excel for bank settlement
    [HttpGet("bank-excel")]
    [Authorize(Policy = Perms.ClaimExport)]
    public async Task<IActionResult> BankExcel()
    {
        var data = await db.Claims
            .Join(db.Assignments.Include(a => a.Course).Include(a => a.Setter),
                  c => c.AssignmentId, a => a.Id, (c, a) => new { c, a })
            .ToListAsync();
        var banks = await db.BankDetails.ToDictionaryAsync(b => b.UserId);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Bank Settlement");
        string[] headers = ["S.No", "Setter Name", "Setter Type", "Course", "Account Holder", "Account Number",
                            "Bank", "Branch", "IFSC", "PAN", "Gross (Rs.)", "TDS (Rs.)", "Net Payable (Rs.)", "Mobile", "E-mail"];
        for (var i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
        var head = ws.Range(1, 1, 1, headers.Length);
        head.Style.Font.Bold = true;
        head.Style.Fill.BackgroundColor = XLColor.FromHtml("#243B6B");
        head.Style.Font.FontColor = XLColor.White;

        var r = 2;
        foreach (var x in data.OrderBy(x => x.a.Setter.Name))
        {
            banks.TryGetValue(x.a.SetterId, out var b);
            ws.Cell(r, 1).Value = r - 1;
            ws.Cell(r, 2).Value = x.a.Setter.Name;
            ws.Cell(r, 3).Value = x.c.SetterType;
            ws.Cell(r, 4).Value = x.a.Course.Code + " - " + x.a.Course.Title;
            ws.Cell(r, 5).Value = b?.AccountHolder ?? "";
            ws.Cell(r, 6).Style.NumberFormat.Format = "@";
            ws.Cell(r, 6).Value = b?.AccountNumber ?? "";   // text: keeps leading zeros
            ws.Cell(r, 7).Value = b?.BankName ?? "";
            ws.Cell(r, 8).Value = b?.Branch ?? "";
            ws.Cell(r, 9).Value = b?.Ifsc ?? "";
            ws.Cell(r, 10).Value = b?.Pan ?? "";
            ws.Cell(r, 11).Value = x.c.Gross;
            ws.Cell(r, 12).Value = x.c.Tds;
            ws.Cell(r, 13).Value = x.c.Net;
            ws.Cell(r, 14).Style.NumberFormat.Format = "@";
            ws.Cell(r, 14).Value = x.a.Setter.Mobile ?? "";
            ws.Cell(r, 15).Value = x.a.Setter.Email;
            r++;
        }

        ws.Cell(r, 10).Value = "TOTAL";
        ws.Cell(r, 10).Style.Font.Bold = true;
        foreach (var col in new[] { 11, 12, 13 })
        {
            var letter = ws.Column(col).ColumnLetter();
            ws.Cell(r, col).FormulaA1 = $"SUM({letter}2:{letter}{Math.Max(2, r - 1)})";
            ws.Cell(r, col).Style.Font.Bold = true;
        }
        ws.Range(2, 11, r, 13).Style.NumberFormat.Format = "#,##0.00";
        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"QP_Claims_Bank_Settlement_{DateTime.Now:yyyyMMdd}.xlsx");
    }
}