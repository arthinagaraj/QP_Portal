using System.Text.RegularExpressions;

namespace QpPortal.Api.Services;

public record UnitInfo(int UnitNo, string Title, string Topics);

public record Analysis(string Btl, string? Verb, decimal MatchScore, string SyllabusVerdict, int? BetterUnit,
                       string Clarity, List<string> Notes, List<string> Matched, List<string> NotInSyllabus);

public static class QuestionAnalyzer
{
    static readonly Dictionary<string, string> VerbLevel = BuildVerbs();

    static Dictionary<string, string> BuildVerbs()
    {
        var d = new Dictionary<string, string>();
        void Add(string level, string words) { foreach (var w in words.Split(' ')) d[w] = level; }
        Add("K1", "define list state name identify recall write mention label what who");
        Add("K2", "explain describe discuss summarize summarise illustrate classify compare differentiate interpret outline");
        Add("K3", "apply solve compute calculate demonstrate use construct implement trace find simulate");
        Add("K4", "analyze analyse examine distinguish test categorize investigate derive");
        Add("K5", "evaluate justify assess criticize recommend judge prove validate");
        Add("K6", "design develop formulate propose create compose devise");
        return d;
    }

    static readonly HashSet<string> Stop = new(("a an the of in on for to and or with is are be by from as at that this these those it its " +
        "into using given which how why when where example examples suitable briefly neat diagram marks following any each " +
        "about between their there then than also can does do give steps step consider problem show").Split(' '));

    static string Key(string w)
    {
        w = Regex.Replace(w.ToLowerInvariant(), "[^a-z0-9]", "");
        w = Regex.Replace(w, "(ing|ed|s)$", "");
        return w.Length > 5 ? w[..5] : w;
    }

    static List<string> Words(string? t) => Regex.Matches(t ?? "", "[A-Za-z0-9]+").Select(m => m.Value).ToList();

    static Dictionary<string, string> ContentWords(string t)
    {
        var d = new Dictionary<string, string>();
        foreach (var w in Words(t))
        {
            var l = w.ToLowerInvariant();
            if (l.Length < 3 || Stop.Contains(l) || VerbLevel.ContainsKey(l)) continue;
            d.TryAdd(Key(l), l);
        }
        return d;
    }

    static HashSet<string> UnitKeys(UnitInfo u) => Words(u.Title + " " + u.Topics).Select(Key).ToHashSet();

    public static Analysis Analyze(string plain, string html, decimal marks, int unitNo, IList<UnitInfo> units)
    {
        var cw = ContentWords(plain);
        var target = units.FirstOrDefault(u => u.UnitNo == unitNo);
        var tKeys = target is null ? new HashSet<string>() : UnitKeys(target);

        var matched = cw.Where(kv => tKeys.Contains(kv.Key)).Select(kv => kv.Value).ToList();
        var notIn = cw.Where(kv => !tKeys.Contains(kv.Key)).Select(kv => kv.Value).ToList();
        decimal score = cw.Count == 0 ? 0 : Math.Round((decimal)matched.Count / cw.Count, 2);
        var verdict = score >= 0.5m ? "In" : "Out";

        int? better = null; decimal best = score;
        foreach (var u in units.Where(u => u.UnitNo != unitNo))
        {
            var k = UnitKeys(u);
            var s = cw.Count == 0 ? 0 : (decimal)cw.Keys.Count(k.Contains) / cw.Count;
            if (s >= 0.5m && s > best + 0.15m) { best = s; better = u.UnitNo; }
        }

        // BTL: highest-level action verb in the question; K2 if none
        var verbs = Words(plain).Select(w => w.ToLowerInvariant()).Where(VerbLevel.ContainsKey)
                                .Select(w => (Word: w, Level: VerbLevel[w])).ToList();
        var top = verbs.OrderByDescending(v => v.Level).FirstOrDefault();
        var btl = verbs.Count == 0 ? "K2" : top.Level;
        var verb = verbs.Count == 0 ? null : top.Word;

        // Clarity: question must be complete, with all required data
        var notes = new List<string>();
        var wc = Words(plain).Count;
        if (wc < 4) notes.Add("Question is too short.");
        if (marks >= 10 && wc < 10) notes.Add($"A {marks}-mark question needs more detail (at least 10 words).");
        if (verbs.Count == 0 && !plain.TrimEnd().EndsWith('?'))
            notes.Add("No action verb (explain, solve, design...) - state what the student must do.");
        var refersToData = Regex.IsMatch(plain,
            @"\b(figure|graph shown|table below|given table|following table|following graph|following figure|shown below|given below)\b",
            RegexOptions.IgnoreCase);
        var hasMedia = Regex.IsMatch(html ?? "", "<(img|table)", RegexOptions.IgnoreCase);
        if (refersToData && !hasMedia) notes.Add("Refers to a figure, table or data that is not included.");
        if (Regex.IsMatch(plain, @"(\.\.\.|___|\?\?|\[\s*\])")) notes.Add("Contains blanks or placeholders.");
        var clarity = notes.Count == 0 ? "Complete" : "Incomplete";

        if (verdict == "Out")
            notes.Insert(0, $"Only {score * 100:0}% of the key terms are from the Unit {unitNo} syllabus (deviation above 50%).");
        if (better is not null) notes.Add($"This question fits Unit {better} better.");

        return new Analysis(btl, verb, score, verdict, better, clarity, notes, matched, notIn);
    }
}