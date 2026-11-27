using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace QpPortal.Api.Services;

public record SuggestedQuestion(string Text, string Btl);
public record SuggestResult(string Source, string? Note, List<SuggestedQuestion> Items);

// Suggests questions for one unit. Uses an LLM (Claude or Gemini) when Llm:ApiKey is set, otherwise syllabus templates.
// Only the course/unit syllabus is sent to the LLM – never the setter's questions.
public class QuestionSuggester(IConfiguration cfg)
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(40) };

    public async Task<SuggestResult> SuggestAsync(string course, int unitNo, string unitTitle, string topics, string co,
                                                  decimal marks, int count, List<string> avoid)
    {
        string? note = null;
        if (!string.IsNullOrWhiteSpace(cfg["Llm:ApiKey"]))
        {
            var (items, err) = await FromLlm(course, unitNo, unitTitle, topics, co, marks, count);
            if (items is { Count: > 0 }) return new SuggestResult(LlmName, null, items);
            note = $"AI service unavailable ({err}) – showing syllabus-based suggestions.";
        }
        return new SuggestResult("Syllabus templates", note, FromTemplates(topics, unitTitle, marks, count, avoid));
    }

    string Provider => (cfg["Llm:Provider"] ?? "anthropic").Trim().ToLowerInvariant();
    string LlmName => Provider == "gemini" ? "AI (Gemini)" : "AI (Claude)";

    async Task<(List<SuggestedQuestion>?, string)> FromLlm(string course, int unitNo, string unitTitle, string topics,
                                                           string co, decimal marks, int count)
    {
        var level = marks <= 3 ? "short-answer questions answerable in 3–4 lines (mostly K1 Remember / K2 Understand)"
                  : marks < 10 ? "medium-length questions (K2 Understand / K3 Apply)"
                  : "long descriptive or problem-solving questions needing a detailed answer, at least 12 words each (K2 to K5, mostly K3 Apply / K4 Analyze)";
        var prompt =
$@"You are an experienced university question paper setter (Anna University style, India).
Course: {course}
Unit {unitNo}: {unitTitle}
Syllabus topics: {topics}
Course outcome: {co}

Write {count} distinct exam questions worth {marks:0.#} marks each: {level}.
Rules: use ONLY the syllabus topics above; each question on a different topic; vary the question type (define, explain, compare, apply, analyze…);
questions must be clear and complete, with no answer and no numbering.
Return ONLY a JSON array, nothing else: [{{""question"": ""..."", ""btl"": ""K2""}}]  (btl is one of K1..K6, Bloom's level).";

        try
        {
            HttpRequestMessage req;
            if (Provider == "gemini")
            {
                var model = cfg["Llm:Model"] ?? "gemini-2.5-flash";
                req = new HttpRequestMessage(HttpMethod.Post, $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent")
                { Content = JsonContent.Create(new { contents = new[] { new { parts = new[] { new { text = prompt } } } } }) };
                req.Headers.Add("x-goog-api-key", cfg["Llm:ApiKey"]);
            }
            else
            {
                var model = cfg["Llm:Model"] ?? "claude-haiku-5-5";
                req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
                { Content = JsonContent.Create(new { model, max_tokens = 1500, messages = new[] { new { role = "user", content = prompt } } }) };
                req.Headers.Add("x-api-key", cfg["Llm:ApiKey"]);
                req.Headers.Add("anthropic-version", "2023-06-01");
            }
            using var resp = await Http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) return (null, $"HTTP {(int)resp.StatusCode}");
            using var doc = JsonDocument.Parse(body);
            var text = Provider == "gemini"
                ? doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString()
                : doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString();
            return (ParseItems(text ?? ""), "no questions in reply");
        }
        catch (Exception ex) { return (null, ex.GetType().Name); }
    }

    public static List<SuggestedQuestion> ParseItems(string text)
    {
        int a = text.IndexOf('['), b = text.LastIndexOf(']');
        if (a < 0 || b <= a) return [];
        try
        {
            using var doc = JsonDocument.Parse(text[a..(b + 1)]);
            return doc.RootElement.EnumerateArray()
                .Select(e => new SuggestedQuestion(
                    (e.TryGetProperty("question", out var q) ? q.GetString() : null)?.Trim() ?? "",
                    e.TryGetProperty("btl", out var k) && Regex.IsMatch(k.GetString() ?? "", "^K[1-6]$") ? k.GetString()! : "K2"))
                .Where(x => x.Text.Length > 8).ToList();
        }
        catch { return []; }
    }

    // ---------- offline: question templates filled with this unit's syllabus topics ----------
    static readonly (string T, string K, bool Two)[] Short =
    [
        ("Define {0}.", "K1", false), ("List any two features of {0}.", "K1", false), ("What is the need for {0}?", "K2", false),
        ("Differentiate {0} and {1}.", "K2", true), ("Give an example of {0} and state its purpose.", "K2", false), ("Mention any two applications of {0}.", "K1", false),
    ];
    static readonly (string T, string K, bool Two)[] Medium =
    [
        ("Explain {0} with a suitable example.", "K2", false), ("Illustrate {0} with a neat diagram.", "K3", false),
        ("Compare {0} and {1} with examples.", "K2", true), ("Demonstrate the use of {0} with a suitable example.", "K3", false),
    ];
    static readonly (string T, string K, bool Two)[] Long =
    [
        ("Explain {0} in detail with a neat diagram and a suitable example.", "K2", false),
        ("Apply {0} to solve a suitable problem and explain each step clearly.", "K3", false),
        ("Compare and contrast {0} and {1} in detail with suitable examples.", "K4", true),
        ("Analyze the advantages and limitations of {0} with suitable examples.", "K4", false),
        ("Evaluate when {0} should be preferred over {1}, justifying your answer with examples.", "K5", true),
    ];

    public static List<SuggestedQuestion> FromTemplates(string topics, string unitTitle, decimal marks, int count, List<string> avoid)
    {
        var all = topics.Split(',', ';').Select(t => t.Trim()).Where(t => t.Length > 2).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (all.Count == 0) all = [unitTitle];
        var used = string.Join(" ", avoid).ToLowerInvariant();
        var fresh = all.Where(t => !used.Contains(t.ToLowerInvariant())).OrderBy(_ => Random.Shared.Next()).ToList();
        var pool = fresh.Count >= 2 ? fresh : all.OrderBy(_ => Random.Shared.Next()).ToList();

        var tpl = (marks <= 3 ? Short : marks < 10 ? Medium : Long).OrderBy(_ => Random.Shared.Next()).ToList();
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // two-topic questions use neighbouring syllabus topics (usually related, e.g. "sequential access, random access")
        var pairs = Enumerable.Range(0, Math.Max(0, all.Count - 1)).Select(i => (all[i], all[i + 1]))
                              .Where(p => !used.Contains(p.Item1.ToLowerInvariant()) || !used.Contains(p.Item2.ToLowerInvariant()))
                              .OrderBy(_ => Random.Shared.Next()).ToList();
        var outList = new List<SuggestedQuestion>();
        for (int ti = 0, pi = 0; outList.Count < count && ti < 40; ti++)
        {
            var (t, k, two) = tpl[ti % tpl.Count];
            if (two)
            {
                var pr = pairs.FirstOrDefault(p => !taken.Contains(p.Item1) && !taken.Contains(p.Item2));
                if (pr == default) continue;
                taken.Add(pr.Item1); taken.Add(pr.Item2);
                outList.Add(new SuggestedQuestion(string.Format(t, pr.Item1, pr.Item2), k));
            }
            else
            {
                while (pi < pool.Count && taken.Contains(pool[pi])) pi++;
                if (pi >= pool.Count) continue;
                taken.Add(pool[pi]);
                outList.Add(new SuggestedQuestion(string.Format(t, pool[pi]), k));
            }
        }
        return outList;
    }
}
