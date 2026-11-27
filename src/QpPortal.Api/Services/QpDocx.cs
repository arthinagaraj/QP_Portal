using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace QpPortal.Api.Services;

public record QpEntry(string SubLabel, string Html, decimal Marks, string Co, string Btl);
public record QpRow(string QLabel, string Option, List<QpEntry> Entries);
public record QpPart(string Heading, List<QpRow> Rows);
public record QpDoc(string College, string ExamLine, string CourseLine, string RegulationLine,
                    string Duration, decimal MaxMarks, string? Instructions, List<QpPart> Parts, byte[]? Logo);

// Builds a real, editable Word (.docx) question paper with no extra package (works in Word and LibreOffice).
public static class QpDocx
{
    const int PageW = 11906, Margin = 1134, ContentW = PageW - 2 * Margin;     // A4, 2 cm margins (twips)
    static readonly int[] Cols = [900, ContentW - 900 - 950 - 750 - 750, 950, 750, 750]; // Q.No | Question | Marks | CO | BTL

    sealed class Ctx
    {
        public readonly List<(string Rid, string Name, byte[] Data)> Media = [];
        public int Pic;
    }

    public static byte[] Build(QpDoc d)
    {
        var c = new Ctx();
        var body = new StringBuilder();

        if (d.Logo is { Length: > 0 }) body.Append(P(ImageRun(c, d.Logo, ContentW * 635L, "png"), "center"));
        body.Append(P(R(d.College, bold: true, size: 28), "center"));
        body.Append(P(R(d.ExamLine, bold: true, size: 24), "center"));
        body.Append(P(R(d.CourseLine, bold: true, size: 24), "center"));
        body.Append(P(R(d.RegulationLine, size: 22), "center"));
        body.Append(TwoCol($"Time: {d.Duration}", $"Maximum Marks: {d.MaxMarks:0.#}"));
        if (!string.IsNullOrWhiteSpace(d.Instructions)) body.Append(P(R(d.Instructions!, italic: true), "center"));

        foreach (var part in d.Parts)
        {
            body.Append(P(R(part.Heading, bold: true, size: 24), "center", before: 200));
            body.Append(QuestionTable(c, part));
        }

        body.Append($"<w:sectPr><w:pgSz w:w=\"{PageW}\" w:h=\"16838\"/><w:pgMar w:top=\"{Margin}\" w:right=\"{Margin}\" w:bottom=\"{Margin}\" w:left=\"{Margin}\" w:header=\"709\" w:footer=\"709\" w:gutter=\"0\"/></w:sectPr>");

        var document = $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture"><w:body>{body}</w:body></w:document>
            """;

        var rels = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
        rels.Append("<Relationship Id=\"rIdStyles\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
        foreach (var m in c.Media)
            rels.Append($"<Relationship Id=\"{m.Rid}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/image\" Target=\"media/{m.Name}\"/>");
        rels.Append("</Relationships>");

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            Add(zip, "[Content_Types].xml", ContentTypes);
            Add(zip, "_rels/.rels", PackageRels);
            Add(zip, "word/document.xml", document);
            Add(zip, "word/styles.xml", Styles);
            Add(zip, "word/_rels/document.xml.rels", rels.ToString());
            foreach (var m in c.Media)
            {
                var e = zip.CreateEntry("word/media/" + m.Name, CompressionLevel.Optimal);
                using var s = e.Open(); s.Write(m.Data);
            }
        }
        return ms.ToArray();
    }

    // ---------------- question table ----------------
    static string QuestionTable(Ctx c, QpPart part)
    {
        var sb = new StringBuilder();
        sb.Append("<w:tbl><w:tblPr><w:tblW w:w=\"" + ContentW + "\" w:type=\"dxa\"/><w:tblLayout w:type=\"fixed\"/>" + Borders("tblBorders") + "</w:tblPr><w:tblGrid>");
        foreach (var w in Cols) sb.Append($"<w:gridCol w:w=\"{w}\"/>");
        sb.Append("</w:tblGrid>");
        sb.Append(Tr([Tc(0, P(R("Q.No.", bold: true), "center")), Tc(1, P(R("Question", bold: true), "center")),
                      Tc(2, P(R("Marks", bold: true), "center")), Tc(3, P(R("CO", bold: true), "center")), Tc(4, P(R("BTL", bold: true), "center"))], header: true));
        foreach (var row in part.Rows)
        {
            if (row.Option == "b")
                sb.Append(Tr([$"<w:tc><w:tcPr><w:tcW w:w=\"{ContentW}\" w:type=\"dxa\"/><w:gridSpan w:val=\"5\"/></w:tcPr>{P(R("(OR)", bold: true), "center")}</w:tc>"]));
            if (row.Entries.Count == 0)
            {
                sb.Append(Tr([Tc(0, P(R(row.QLabel), "center")), Tc(1, P(R("[not entered]", italic: true))), Tc(2, P("")), Tc(3, P("")), Tc(4, P(""))]));
                continue;
            }
            for (var i = 0; i < row.Entries.Count; i++)
            {
                var e = row.Entries[i];
                var prefix = e.SubLabel == "" ? "" : $"({e.SubLabel}) ";
                sb.Append(Tr([
                    Tc(0, P(R(i == 0 ? row.QLabel : ""), "center")),
                    Tc(1, HtmlToBlocks(c, e.Html, prefix)),
                    Tc(2, P(R($"{e.Marks:0.#}"), "center")),
                    Tc(3, P(R(e.Co), "center")),
                    Tc(4, P(R(e.Btl), "center"))]));
            }
        }
        sb.Append("</w:tbl>");
        return sb.ToString();
    }

    static string Tr(string[] cells, bool header = false) =>
        "<w:tr>" + (header ? "<w:trPr><w:tblHeader/></w:trPr>" : "") + string.Concat(cells) + "</w:tr>";
    static string Tc(int col, string content) =>
        $"<w:tc><w:tcPr><w:tcW w:w=\"{Cols[col]}\" w:type=\"dxa\"/></w:tcPr>{content}</w:tc>";
    static string Borders(string tag) =>
        $"<w:{tag}>" + string.Concat(new[] { "top", "left", "bottom", "right", "insideH", "insideV" }
            .Select(b => $"<w:{b} w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"000000\"/>")) + $"</w:{tag}>";

    static string TwoCol(string left, string right) =>
        $"<w:tbl><w:tblPr><w:tblW w:w=\"{ContentW}\" w:type=\"dxa\"/><w:tblLayout w:type=\"fixed\"/></w:tblPr><w:tblGrid><w:gridCol w:w=\"{ContentW / 2}\"/><w:gridCol w:w=\"{ContentW / 2}\"/></w:tblGrid>" +
        $"<w:tr><w:tc><w:tcPr><w:tcW w:w=\"{ContentW / 2}\" w:type=\"dxa\"/></w:tcPr>{P(R(left, bold: true))}</w:tc>" +
        $"<w:tc><w:tcPr><w:tcW w:w=\"{ContentW / 2}\" w:type=\"dxa\"/></w:tcPr>{P(R(right, bold: true), "right")}</w:tc></w:tr></w:tbl>";

    // ---------------- paragraphs and runs ----------------
    static string P(string runs, string? align = null, int before = 0) =>
        "<w:p><w:pPr>" + (before > 0 ? $"<w:spacing w:before=\"{before}\" w:after=\"60\"/>" : "<w:spacing w:after=\"60\"/>") +
        (align is null ? "" : $"<w:jc w:val=\"{align}\"/>") + "</w:pPr>" + runs + "</w:p>";

    static string R(string text, bool bold = false, bool italic = false, bool sub = false, bool sup = false,
                    bool underline = false, int size = 0, bool math = false)
    {
        if (text.Length == 0) return "";
        var pr = new StringBuilder();
        if (math) pr.Append("<w:rFonts w:ascii=\"Cambria Math\" w:hAnsi=\"Cambria Math\"/>");
        if (bold) pr.Append("<w:b/>");
        if (italic || math) pr.Append("<w:i/>");
        if (underline) pr.Append("<w:u w:val=\"single\"/>");
        if (size > 0) pr.Append($"<w:sz w:val=\"{size}\"/>");
        if (sub) pr.Append("<w:vertAlign w:val=\"subscript\"/>");
        if (sup) pr.Append("<w:vertAlign w:val=\"superscript\"/>");
        return $"<w:r>{(pr.Length > 0 ? "<w:rPr>" + pr + "</w:rPr>" : "")}<w:t xml:space=\"preserve\">{Xml(text)}</w:t></w:r>";
    }

    static string Xml(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    // ---------------- HTML (from the question editor) -> Word blocks ----------------
    static string HtmlToBlocks(Ctx c, string html, string prefix = "")
    {
        html = FormulasToMarkers(html ?? "");
        var blocks = new StringBuilder();
        var runs = new StringBuilder(prefix == "" ? "" : R(prefix, bold: true));
        bool b = false, i = false, u = false, sub = false, sup = false;
        void Flush() { if (runs.Length > 0) { blocks.Append(P(runs.ToString())); runs.Clear(); } }

        var tokens = Regex.Matches(html, "<[^>]+>|[^<]+");
        for (var k = 0; k < tokens.Count; k++)
        {
            var t = tokens[k].Value;
            if (!t.StartsWith('<')) { runs.Append(R(WebUtility.HtmlDecode(t), b, i, sub, sup, u)); continue; }

            var m = Regex.Match(t, @"^<\s*(/?)\s*([a-zA-Z0-9]+)");
            if (!m.Success) continue;
            var close = m.Groups[1].Value == "/";
            var name = m.Groups[2].Value.ToLowerInvariant();
            switch (name)
            {
                case "b": case "strong": b = !close; break;
                case "i": case "em": i = !close; break;
                case "u": u = !close; break;
                case "sub": sub = !close; break;
                case "sup": sup = !close; break;
                case "br": Flush(); break;
                case "div": case "p": case "li": case "h1": case "h2": case "h3": case "h4": Flush(); break;
                case "qpformula":
                    if (!close && k + 1 < tokens.Count && !tokens[k + 1].Value.StartsWith('<'))
                    { runs.Append(LatexToRuns(WebUtility.HtmlDecode(tokens[k + 1].Value))); k++; }
                    break;
                case "img":
                    if (close) break;
                    var src = Regex.Match(t, "src=\"data:image/([a-zA-Z+.-]+);base64,([^\"]+)\"");
                    if (src.Success && src.Groups[1].Value is "png" or "jpeg" or "jpg" or "gif")
                    {
                        Flush();
                        try { blocks.Append(P(ImageRun(c, Convert.FromBase64String(src.Groups[2].Value), Cols[1] * 635L - 200_000, src.Groups[1].Value), "center")); }
                        catch { runs.Append(R("[image]", italic: true)); }
                    }
                    else runs.Append(R("[image]", italic: true));
                    break;
                case "table":
                    if (close) break;
                    Flush();
                    var end = FindClose(tokens, k, "table");
                    var inner = string.Concat(Enumerable.Range(k + 1, Math.Max(0, end - k - 1)).Select(x => tokens[x].Value));
                    blocks.Append(NestedTable(c, inner));
                    k = end;
                    break;
            }
        }
        Flush();
        return blocks.Length > 0 ? blocks.ToString() : P("");
    }

    static int FindClose(MatchCollection tokens, int start, string tag)
    {
        var depth = 0;
        for (var k = start; k < tokens.Count; k++)
        {
            var t = tokens[k].Value;
            if (Regex.IsMatch(t, $"^<\\s*{tag}\\b", RegexOptions.IgnoreCase)) depth++;
            else if (Regex.IsMatch(t, $"^<\\s*/\\s*{tag}\\b", RegexOptions.IgnoreCase) && --depth == 0) return k;
        }
        return tokens.Count - 1;
    }

    static string NestedTable(Ctx c, string inner)
    {
        var rows = Regex.Matches(inner, @"<tr\b[^>]*>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase)
            .Select(r => Regex.Matches(r.Groups[1].Value, @"<t[dh]\b[^>]*>(.*?)</t[dh]>", RegexOptions.Singleline | RegexOptions.IgnoreCase)
                              .Select(x => x.Groups[1].Value).ToList())
            .Where(r => r.Count > 0).ToList();
        if (rows.Count == 0) return "";
        var cols = rows.Max(r => r.Count);
        var w = (Cols[1] - 200) / cols;
        var sb = new StringBuilder($"<w:tbl><w:tblPr><w:tblW w:w=\"{w * cols}\" w:type=\"dxa\"/><w:jc w:val=\"center\"/>{Borders("tblBorders")}</w:tblPr><w:tblGrid>");
        for (var k = 0; k < cols; k++) sb.Append($"<w:gridCol w:w=\"{w}\"/>");
        sb.Append("</w:tblGrid>");
        foreach (var r in rows)
        {
            sb.Append("<w:tr>");
            for (var k = 0; k < cols; k++)
                sb.Append($"<w:tc><w:tcPr><w:tcW w:w=\"{w}\" w:type=\"dxa\"/></w:tcPr>{HtmlToBlocks(c, k < r.Count ? r[k] : "")}</w:tc>");
            sb.Append("</w:tr>");
        }
        sb.Append("</w:tbl>");
        return sb.ToString() + P("");   // Word needs a paragraph after a table inside a cell
    }

    // KaTeX formula spans -> <qpformula>LaTeX</qpformula> (shown in Cambria Math; Word can convert LaTeX to an equation)
    public static string FormulasToMarkers(string html)
    {
        var sb = new StringBuilder();
        var pos = 0;
        var open = new Regex("<span[^>]*class=\"formula\"[^>]*>", RegexOptions.IgnoreCase);
        var tag = new Regex("<(/?)span\\b[^>]*>", RegexOptions.IgnoreCase);
        while (true)
        {
            var m = open.Match(html, pos);
            if (!m.Success) { sb.Append(html, pos, html.Length - pos); break; }
            sb.Append(html, pos, m.Index - pos);
            int depth = 1, end = html.Length;
            foreach (Match t in tag.Matches(html, m.Index + m.Length))
            {
                depth += t.Groups[1].Value == "/" ? -1 : 1;
                if (depth == 0) { end = t.Index + t.Length; break; }
            }
            var inner = html.Substring(m.Index, end - m.Index);
            var tex = Regex.Match(m.Value, "data-tex=\"([^\"]*)\"").Groups[1].Value;
            if (tex == "") tex = Regex.Match(inner, "<annotation encoding=\"application/x-tex\">(.*?)</annotation>", RegexOptions.Singleline).Groups[1].Value;
            sb.Append($"<qpformula>{tex}</qpformula>");
            pos = end;
        }
        return sb.ToString();
    }

    // ---------------- LaTeX -> readable, editable Word text (sub/superscripts, symbols) ----------------
    static readonly (string Cmd, string Sym)[] Symbols =
    [
        ("\\rightarrow", "→"), ("\\Rightarrow", "⇒"), ("\\leftarrow", "←"), ("\\infty", "∞"), ("\\approx", "≈"),
        ("\\times", "×"), ("\\div", "÷"), ("\\cdot", "·"), ("\\le", "≤"), ("\\ge", "≥"), ("\\ne", "≠"), ("\\pm", "±"),
        ("\\sum", "∑"), ("\\prod", "∏"), ("\\int", "∫"), ("\\in", "∈"), ("\\forall", "∀"), ("\\exists", "∃"), ("\\to", "→"),
        ("\\alpha", "α"), ("\\beta", "β"), ("\\gamma", "γ"), ("\\delta", "δ"), ("\\theta", "θ"), ("\\lambda", "λ"),
        ("\\mu", "μ"), ("\\sigma", "σ"), ("\\pi", "π"), ("\\omega", "ω"), ("\\Delta", "Δ"), ("\\Omega", "Ω"), ("\\Sigma", "Σ"),
        ("\\log", "log"), ("\\ln", "ln"), ("\\sin", "sin"), ("\\cos", "cos"), ("\\tan", "tan"), ("\\lim", "lim"),
        ("\\left", ""), ("\\right", ""), ("\\,", " "), ("\\;", " "), ("\\ ", " "), ("\\quad", "  "), ("\\\\", " ; "), ("&", " "),
    ];

    static int Brace(string s, int open)          // index of the matching '}' for s[open] == '{'
    {
        var depth = 0;
        for (var k = open; k < s.Length; k++)
        {
            if (s[k] == '{') depth++;
            else if (s[k] == '}' && --depth == 0) return k;
        }
        return s.Length - 1;
    }

    static string Group(string s, ref int k)       // reads {...} or a single character after position k
    {
        while (k < s.Length && s[k] == ' ') k++;
        if (k >= s.Length) return "";
        if (s[k] == '{') { var e = Brace(s, k); var g = s.Substring(k + 1, e - k - 1); k = e + 1; return g; }
        return s[k++].ToString();
    }

    static string Wrap(string x) => x.Length <= 1 ? x : "(" + x + ")";

    static string Linear(string s)                 // \frac, \sqrt, \text, \overline, \vec -> plain text, keeps _ and ^
    {
        var sb = new StringBuilder();
        for (var k = 0; k < s.Length;)
        {
            if (s[k] == '\\')
            {
                string? cmd = null;
                foreach (var c in new[] { "\\frac", "\\dfrac", "\\sqrt", "\\text", "\\mathrm", "\\overline", "\\bar", "\\vec", "\\hat" })
                    if (string.CompareOrdinal(s, k, c, 0, c.Length) == 0 && (k + c.Length >= s.Length || !char.IsLetter(s[k + c.Length]))) { cmd = c; break; }
                if (cmd is not null)
                {
                    k += cmd.Length;
                    switch (cmd)
                    {
                        case "\\frac": case "\\dfrac":
                            var num = Linear(Group(s, ref k)); var den = Linear(Group(s, ref k));
                            sb.Append(Wrap(num)).Append('/').Append(Wrap(den)); break;
                        case "\\sqrt":
                            var idx = "";
                            if (k < s.Length && s[k] == '[') { var e = s.IndexOf(']', k); idx = s.Substring(k + 1, e - k - 1); k = e + 1; }
                            sb.Append(idx == "" ? "√" : "^{" + idx + "}√").Append(Wrap(Linear(Group(s, ref k)))); break;
                        case "\\overline": case "\\bar": sb.Append(Linear(Group(s, ref k))).Append('̅'); break;
                        case "\\vec": sb.Append(Linear(Group(s, ref k))).Append('⃗'); break;
                        case "\\hat": sb.Append(Linear(Group(s, ref k))).Append('̂'); break;
                        default: sb.Append(Group(s, ref k)); break;
                    }
                    continue;
                }
            }
            sb.Append(s[k++]);
        }
        var r = sb.ToString();
        foreach (var (cmd, sym) in Symbols.OrderByDescending(x => x.Cmd.Length)) r = Regex.Replace(r, Regex.Escape(cmd) + (char.IsLetter(cmd[^1]) ? "(?![a-zA-Z])" : ""), sym);
        return r;
    }

    static string LatexToRuns(string tex)
    {
        var s = Linear(tex);
        var runs = new StringBuilder();
        var plain = new StringBuilder();
        void Out() { if (plain.Length > 0) { runs.Append(R(plain.ToString(), math: true)); plain.Clear(); } }
        for (var k = 0; k < s.Length;)
        {
            if ((s[k] == '_' || s[k] == '^') && k + 1 < s.Length)
            {
                var isSub = s[k] == '_'; k++;
                var g = Group(s, ref k).Replace("{", "").Replace("}", "");
                Out();
                runs.Append(R(g, sub: isSub, sup: !isSub, math: true));
                continue;
            }
            if (s[k] != '{' && s[k] != '}') plain.Append(s[k]);
            k++;
        }
        Out();
        return runs.ToString();
    }

    // ---------------- images ----------------
    static string ImageRun(Ctx c, byte[] data, long maxWidthEmu, string type)
    {
        var (wpx, hpx) = ImageSize(data);
        long cx = wpx * 9525L, cy = hpx * 9525L;
        if (cx > maxWidthEmu) { cy = cy * maxWidthEmu / cx; cx = maxWidthEmu; }
        c.Pic++;
        var ext = type is "jpeg" or "jpg" ? "jpeg" : type;
        var rid = $"rIdImg{c.Pic}";
        c.Media.Add((rid, $"image{c.Pic}.{ext}", data));
        return $"<w:r><w:drawing><wp:inline distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\"><wp:extent cx=\"{cx}\" cy=\"{cy}\"/><wp:docPr id=\"{c.Pic}\" name=\"Picture {c.Pic}\"/>" +
               "<a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/picture\"><pic:pic>" +
               $"<pic:nvPicPr><pic:cNvPr id=\"{c.Pic}\" name=\"image{c.Pic}\"/><pic:cNvPicPr/></pic:nvPicPr>" +
               $"<pic:blipFill><a:blip r:embed=\"{rid}\"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill>" +
               $"<pic:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"{cx}\" cy=\"{cy}\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></pic:spPr>" +
               "</pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r>";
    }

    static (int W, int H) ImageSize(byte[] d)
    {
        try
        {
            if (d.Length > 24 && d[0] == 0x89 && d[1] == 0x50)                       // PNG
                return ((d[16] << 24) | (d[17] << 16) | (d[18] << 8) | d[19], (d[20] << 24) | (d[21] << 16) | (d[22] << 8) | d[23]);
            if (d.Length > 10 && d[0] == 0x47 && d[1] == 0x49)                       // GIF
                return (d[6] | (d[7] << 8), d[8] | (d[9] << 8));
            if (d.Length > 4 && d[0] == 0xFF && d[1] == 0xD8)                        // JPEG
            {
                var p = 2;
                while (p + 9 < d.Length)
                {
                    if (d[p] != 0xFF) { p++; continue; }
                    var marker = d[p + 1];
                    var len = (d[p + 2] << 8) | d[p + 3];
                    if (marker is >= 0xC0 and <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
                        return ((d[p + 7] << 8) | d[p + 8], (d[p + 5] << 8) | d[p + 6]);
                    p += 2 + len;
                }
            }
        }
        catch { }
        return (400, 300);
    }

    static void Add(ZipArchive zip, string path, string content)
    {
        var e = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var w = new StreamWriter(e.Open(), new UTF8Encoding(false));
        w.Write(content);
    }

    const string ContentTypes = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Default Extension="png" ContentType="image/png"/>
          <Default Extension="jpeg" ContentType="image/jpeg"/>
          <Default Extension="gif" ContentType="image/gif"/>
          <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
          <Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/>
        </Types>
        """;

    const string PackageRels = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
        </Relationships>
        """;

    const string Styles = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
          <w:docDefaults>
            <w:rPrDefault><w:rPr><w:rFonts w:ascii="Times New Roman" w:hAnsi="Times New Roman" w:cs="Times New Roman" w:eastAsia="Times New Roman"/><w:sz w:val="22"/><w:szCs w:val="22"/><w:lang w:val="en-IN"/></w:rPr></w:rPrDefault>
            <w:pPrDefault><w:pPr><w:spacing w:after="60" w:line="259" w:lineRule="auto"/></w:pPr></w:pPrDefault>
          </w:docDefaults>
          <w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/></w:style>
        </w:styles>
        """;
}
