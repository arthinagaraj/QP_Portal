using QpPortal.Api.Data;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace QpPortal.Api.Services;

public static class ClaimPdf
{
    public static byte[] Build(string course, string session, DateTime? submittedAt, User u, BankDetail b, QpClaim c)
    {
        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(x => x.FontSize(10));

            page.Header().Column(col =>
            {
                col.Item().AlignCenter().Text("[ College Name ]").FontSize(16).Bold();
                col.Item().AlignCenter().Text("Office of the Controller of Examinations");
                col.Item().PaddingTop(8).AlignCenter().Text("REMUNERATION CLAIM - QUESTION PAPER SETTING").FontSize(12).Bold();
                col.Item().PaddingTop(4).LineHorizontal(1);
            });

            page.Content().PaddingVertical(12).Column(col =>
            {
                col.Spacing(10);

                col.Item().Element(x => Table(x, [
                    ("Course", course),
                    ("Exam session", session),
                    ("QP submitted on", submittedAt?.ToLocalTime().ToString("dd-MM-yyyy HH:mm") ?? "-")]));

                col.Item().Text("Setter details").Bold().FontSize(11);
                col.Item().Element(x => Table(x, [
                    ("Name", u.Name),
                    ("Designation / Department", $"{u.Designation} / {u.Department}"),
                    ("Institution", u.Institution ?? "-"),
                    ("Setter type", u.SetterType ?? "-"),
                    ("E-mail / Mobile", $"{u.Email} / {u.Mobile}"),
                    ("PAN", b.Pan),
                    ("Address", b.Address)]));

                col.Item().Text("Bank account details").Bold().FontSize(11);
                col.Item().Element(x => Table(x, [
                    ("Account holder", b.AccountHolder),
                    ("Account number", b.AccountNumber),
                    ("Bank / Branch", $"{b.BankName} / {b.Branch}"),
                    ("IFSC", b.Ifsc)]));

                col.Item().Text("Claim").Bold().FontSize(11);
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(cd => { cd.RelativeColumn(3); cd.RelativeColumn(1); });
                    void Row(string k, string v, bool bold = false)
                    {
                        var left = t.Cell().Border(0.5f).Padding(5).Text(k);
                        var right = t.Cell().Border(0.5f).Padding(5).AlignRight().Text(v);
                        if (bold) { left.Bold(); right.Bold(); }
                    }
                    Row("Question paper setting fee", $"{c.QpFee:N2}");
                    Row("Travel / other allowance", $"{c.TravelAllowance:N2}");
                    Row("Gross amount", $"{c.Gross:N2}", true);
                    Row($"Less: TDS @ {c.TdsPercent:0.##}% on fee", $"{c.Tds:N2}");
                    Row("NET AMOUNT PAYABLE (Rs.)", $"{c.Net:N2}", true);
                });
                col.Item().Text($"Amount in words: Rupees {InWords(c.Net)} only").Italic();

                col.Item().PaddingTop(6).Text(
                    "Declaration: I declare that the question paper submitted is original, prepared as per the syllabus " +
                    "and the prescribed framework, and that the bank details furnished above are correct.").FontSize(9);

                col.Item().PaddingTop(48).Row(r =>
                {
                    r.RelativeItem().Text("Signature of Setter");
                    r.RelativeItem().AlignCenter().Text("HoD");
                    r.RelativeItem().AlignRight().Text("Controller of Examinations");
                });
            });

            page.Footer().AlignCenter().Text($"Generated {DateTime.Now:dd-MM-yyyy HH:mm} - QP Portal").FontSize(8);
        })).GeneratePdf();
    }

    static void Table(IContainer container, (string Key, string Value)[] rows) =>
        container.Table(t =>
        {
            t.ColumnsDefinition(cd => { cd.ConstantColumn(170); cd.RelativeColumn(); });
            foreach (var (k, v) in rows)
            {
                t.Cell().Border(0.5f).Background(Colors.Grey.Lighten4).Padding(5).Text(k).Bold();
                t.Cell().Border(0.5f).Padding(5).Text(v ?? "-");
            }
        });

    public static string InWords(decimal amount)
    {
        var n = (int)Math.Round(amount);
        if (n == 0) return "Zero";
        string[] a = "One Two Three Four Five Six Seven Eight Nine Ten Eleven Twelve Thirteen Fourteen Fifteen Sixteen Seventeen Eighteen Nineteen".Split(' ');
        string[] b = "_ _ Twenty Thirty Forty Fifty Sixty Seventy Eighty Ninety".Split(' ');
        string Two(int x) => x < 20 ? (x > 0 ? a[x - 1] : "") : b[x / 10] + (x % 10 > 0 ? " " + a[x % 10 - 1] : "");
        string Three(int x) => (x > 99 ? a[x / 100 - 1] + " Hundred" + (x % 100 > 0 ? " " : "") : "") + Two(x % 100);
        var parts = new List<string>();
        foreach (var (v, label) in new[] { (10000000, "Crore"), (100000, "Lakh"), (1000, "Thousand") })
        {
            var q = n / v;
            if (q > 0) { parts.Add(Two(q) + " " + label); n %= v; }
        }
        if (n > 0) parts.Add(Three(n));
        return string.Join(" ", parts);
    }
}