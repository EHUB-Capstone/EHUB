using System.Globalization;
using System.Text.RegularExpressions;
using EHub.Contracts.Workspaces;
using EHub.Shared.Constants;

namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

/// <summary>
/// Finds, without the model, the lines of a file that report where work stands: "2. IN PROGRESS: …" on a review
/// slide, or "Beta Launch In Progress Public beta preparation" in a traction table. For a progress review these are
/// what a lecturer reads first, and a small model often drops them or loses the status while summarising.
/// </summary>
internal static partial class CheckpointStatusLines
{
    public const string Done = "Done";
    public const string InProgress = "InProgress";
    public const string Pending = "Pending";

    private const int MaximumItems = 8;
    private const int MaximumTableRowWords = 12;
    // How far back a numbered request ("1.Thêm chức năng…") is looked for above its numbered status ("1. PENDING: …").
    private const int RequestReach = 15;
    private const int AcronymLength = 3;

    public static IReadOnlyList<WorkspaceStatusItem> Find(string text)
    {
        var lines = CheckpointSummaryText.Sentences(text);
        var items = new List<WorkspaceStatusItem>();
        for (var i = 0; i < lines.Count && items.Count < MaximumItems; i++)
        {
            var labelled = Labelled().Match(lines[i]);
            if (labelled.Success)
            {
                var detail = labelled.Groups["text"].Value.Trim();
                // A status on its own line ("DONE") reports on the explanation that follows it.
                if (detail.Length == 0 && i + 1 < lines.Count && !Labelled().IsMatch(lines[i + 1])) detail = lines[++i];
                if (detail.Length > 0)
                    items.Add(Item(labelled.Groups["status"].Value, detail, RequestFor(lines, i, labelled.Groups["number"].Value)));
                continue;
            }
            if (lines[i].Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > MaximumTableRowWords) continue;
            var row = TableRow().Match(lines[i]);
            if (!row.Success) continue;
            var rest = row.Groups["text"].Value.Trim();
            items.Add(Item(row.Groups["status"].Value,
                rest.Length == 0 ? row.Groups["item"].Value.Trim() : $"{row.Groups["item"].Value.Trim()} — {rest}"));
        }
        return items;
    }

    private static WorkspaceStatusItem Item(string status, string text, string? request = null) => new()
    {
        Status = Normalise(status),
        Text = CheckpointSummaryText.Clip(SentenceCase(text), CheckpointSummaryLimits.MaxEvidenceCharacters),
        Request = request is null ? null : CheckpointSummaryText.Clip(SentenceCase(request), CheckpointSummaryLimits.MaxEvidenceCharacters)
    };

    // Words too common to show that a summary sentence and a status line are about the same piece of work.
    private static readonly HashSet<string> CommonWords = new(StringComparer.Ordinal)
    {
        "nhom", "team", "hoan", "thanh", "da", "dang", "se", "va", "cua", "cac", "cho", "voi", "khi", "sau", "viec", "du",
        "an", "he", "thong", "la", "duoc", "de", "trong", "tren", "nay", "mot", "nhung", "ve", "tu", "den", "theo", "co",
        "khong", "xin", "ghi", "nhan", "the", "and", "of", "to", "in", "a", "for", "with", "on"
    };

    private const int MinimumSharedWords = 4;
    private const int MinimumWordLength = 2;

    // A sentence about one piece of work must share a good part of that work's words, not only four of them:
    // "kho lưu trữ ý tưởng cho các kỳ sau" shares "kỳ", "động", "ý", "tưởng" with another item's long request.
    private const double MinimumSharedShare = 0.4;

    /// <summary>
    /// True when a summary sentence calls finished ("đã", "triển khai", "hoàn thành") a piece of work the file reports
    /// as pending or in progress. A small model merged "1. PENDING" and "2. IN PROGRESS" into "đã xác nhận… đồng thời
    /// triển khai…"; the status list next to the summary is right, so the contradicting sentence is dropped.
    /// </summary>
    public static bool Contradicts(string sentence, IReadOnlyCollection<WorkspaceStatusItem> items)
    {
        if (items.Count == 0 || !CompletionWords().IsMatch(sentence)) return false;
        var words = Words(sentence);
        return items.Where(item => item.Status != Done).Any(item =>
        {
            var own = Words(item.Text + " " + item.Request);
            var shared = own.Count(words.Contains);
            return shared >= MinimumSharedWords && shared >= MinimumSharedShare * own.Count;
        });
    }

    private static HashSet<string> Words(string text) =>
        EHub.Application.Features.Mentoring.Evidence.MentorEvidenceText.Normalize(text)
            .Split([' ', ',', '.', ':', ';', '(', ')', '-', '/', '"'], StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.Length > MinimumWordLength && !CommonWords.Contains(word))
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// The request a numbered status answers: on a review slide the reviewer's points are numbered and the team's
    /// replies use the same numbers ("1.Thêm chức năng…" … "1. PENDING: …"). Without it a reader sees that something is
    /// pending but not what.
    /// </summary>
    private static string? RequestFor(IReadOnlyList<string> lines, int statusLine, string number)
    {
        if (number.Length == 0) return null;
        for (var i = statusLine - 1; i >= 0 && i >= statusLine - RequestReach; i--)
        {
            var request = NumberedLine().Match(lines[i]);
            if (request.Success && request.Groups["number"].Value == number && !Labelled().IsMatch(lines[i]))
                return request.Groups["text"].Value.Trim();
        }
        return null;
    }

    /// <summary>
    /// Text written in capitals ("NHÓM ĐÃ TIẾP THU VÀ CẬP NHẬT REPORT 3") is hard to read in a list, so it is shown in
    /// sentence case. Short ASCII words such as AI or MVP keep their capitals.
    /// </summary>
    private static readonly HashSet<string> Acronyms = new(StringComparer.Ordinal)
    {
        "AI", "MVP", "API", "KPI", "VPS", "R2", "PDF", "SQL", "UI", "UX", "IT", "CEO", "CTO", "CFO", "CMO", "CPO", "VND", "ARR",
        "MRR", "LTV", "CAC", "SAM", "TAM", "SOM", "FPT", "SHA-256", "PENDING", "DONE"
    };

    // Vietnamese words in capitals are accent-free ASCII only when they are very short ("VI", "DE"), so a short
    // ASCII word is an acronym only when it is a known one or contains a digit: "THU" and "XIN" are words.
    private static bool IsAcronym(string word)
    {
        var bare = word.Trim('(', ')', ',', '.', ':', ';');
        return Acronyms.Contains(bare) || (bare.Length <= AcronymLength && bare.Any(char.IsDigit));
    }

    internal static string SentenceCase(string text)
    {
        var letters = text.Where(char.IsLetter).ToArray();
        if (letters.Length == 0 || letters.Count(char.IsUpper) < 0.7 * letters.Length) return text;
        var words = text.Split(' ').Select(word =>
            IsAcronym(word) ? word : word.ToLower(CultureInfo.InvariantCulture)).ToArray();
        var sentence = string.Join(' ', words);
        var first = sentence.TakeWhile(character => !char.IsLetter(character)).Count();
        return first < sentence.Length
            ? sentence[..first] + char.ToUpper(sentence[first], CultureInfo.InvariantCulture) + sentence[(first + 1)..]
            : sentence;
    }

    private static string Normalise(string status) => status.ToLower(CultureInfo.InvariantCulture) switch
    {
        "in progress" or "đang làm" or "đang thực hiện" => InProgress,
        "pending" or "todo" or "chưa làm" or "chưa bắt đầu" => Pending,
        _ => Done
    };

    // Upper case only: on slides a status label is written in capitals, while "done" in a sentence is ordinary prose.
    [GeneratedRegex(@"^(?:(?<number>\d{1,2})[.)]\s*)?(?<status>DONE|PENDING|IN PROGRESS|TODO|COMPLETED|ĐÃ XONG|HOÀN THÀNH|ĐANG LÀM|ĐANG THỰC HIỆN|CHƯA LÀM)\b\s*[:\-–]?\s*(?<text>.*)$")]
    private static partial Regex Labelled();

    // A short table row whose second column is a status: "Market Research Completed Problem validated". English only:
    // Vietnamese "hoàn thành" inside a row is ordinary prose ("Đo lường hiệu quả thay vì chỉ hoàn thành khóa học").
    [GeneratedRegex(@"^(?<item>\p{L}[^:]{1,40}?)\s+(?<status>completed|in progress|pending|done)\b\s*(?<text>.*)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TableRow();

    [GeneratedRegex(@"^(?<number>\d{1,2})[.)]\s*(?<text>\S.*)$")]
    private static partial Regex NumberedLine();

    [GeneratedRegex(@"\bđã\b|hoàn thành|hoàn tất|triển khai|xác nhận|\bcompleted\b|\bdone\b|\blaunched\b|\bimplemented\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CompletionWords();
}
