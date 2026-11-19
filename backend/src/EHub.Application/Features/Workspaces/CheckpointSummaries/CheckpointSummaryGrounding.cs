using System.Text.RegularExpressions;
using EHub.Application.Features.Mentoring.Evidence;

namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

/// <summary>
/// Checks that a sentence written by the model is backed by the file's own words. Small models fill gaps with
/// plausible details ("3 modules", "entrepreneurs"); a key point is kept only when its numbers appear in the file and
/// either most of its words or several distinctive terms (names, acronyms, English terms of slides) do. This catches
/// invented specifics, not a wrong reading of real words, so the screen still says the summary may be inaccurate.
/// </summary>
internal sealed partial class CheckpointSummaryGrounding
{
    private const double MinimumWordSupport = 0.5;

    // A file that is mostly images gives the model only titles, so a detailed key point is likelier invented than read.
    private const double MinimumWordSupportLittleText = 0.75;
    private const int MinimumDistinctiveTerms = 2;

    // Function words carry no claim; leaving them in would make any sentence look supported.
    private static readonly HashSet<string> FunctionWords = new(StringComparer.Ordinal)
    {
        "và", "hoặc", "của", "có", "các", "những", "là", "được", "để", "cho", "với", "trong", "trên", "về", "từ", "đến",
        "theo", "này", "đó", "một", "nhiều", "đã", "sẽ", "đang", "bao", "gồm", "như", "khi", "thì", "mà", "nhằm", "cũng",
        "tài", "liệu", "nội", "dung", "chính", "the", "and", "of", "for", "to", "in", "a", "an", "with", "on", "by"
    };

    private static readonly HashSet<string> CountedUnits = new(StringComparer.Ordinal)
    {
        "người", "nhân", "khách", "học", "sinh", "doanh", "nhà", "cửa", "users", "user", "people", "customers", "students", "companies", "stores"
    };

    private static readonly HashSet<string> MoneyUnits = new(StringComparer.Ordinal)
        { "đồng", "vnd", "vnđ", "đ", "usd", "k", "nghìn", "ngàn", "triệu", "tỷ", "đô" };

    private readonly HashSet<string> _words;
    private readonly IReadOnlyList<string> _sentences;
    private readonly string _text;
    private readonly string _plain;
    private readonly double _minimumWordSupport;
    private readonly bool _littleText;
    private readonly Dictionary<string, string?> _evidence = new(StringComparer.Ordinal);
    private HashSet<string>? _percentages;

    /// <param name="littleText">The file is mostly images: key points need more of their words in the text that was read.</param>
    public CheckpointSummaryGrounding(string sourceText, bool littleText = false)
    {
        _words = Words(sourceText).ToHashSet(StringComparer.Ordinal);
        _sentences = CheckpointSummaryText.Sentences(sourceText);
        _text = " " + CheckpointSummaryText.Normalize(sourceText) + " ";
        _plain = " " + MentorEvidenceText.Normalize(sourceText) + " ";
        _minimumWordSupport = littleText ? MinimumWordSupportLittleText : MinimumWordSupport;
        _littleText = littleText;
    }

    public bool Supports(string sentence)
    {
        if (MisusesMarketSize(sentence) || QuotesSlogan(sentence) || InventsCurrency(sentence) || ReadsMoneyAsCount(sentence)) return false;
        // "Giải pháp: …" — the label the model puts in front is not a claim about the file.
        var claim = Label().Replace(sentence, string.Empty, 1);
        var words = Words(claim).Where(word => !FunctionWords.Contains(word)).Distinct(StringComparer.Ordinal).ToArray();
        if (words.Length == 0) return false;
        // An invented number is the most misleading detail a summary can contain.
        if (words.Any(word => word.Any(char.IsDigit) && !_words.Contains(word))) return false;
        // "15-30 phút" built from a "15" and a "30" that the file never writes together is just as invented.
        if (InventsRange(claim)) return false;
        // Word matching alone loses the minus sign and can split a decimal into unrelated tokens.
        foreach (var percentage in PercentageValues(claim))
        {
            _percentages ??= _sentences.SelectMany(PercentageValues).ToHashSet(StringComparer.Ordinal);
            if (!_percentages.Contains(percentage)) return false;
        }
        var supported = words.Count(_words.Contains);
        if (supported >= _minimumWordSupport * words.Length) return true;
        // Slides are often in English while the summary is in Vietnamese: then distinctive terms carry the evidence. An English
        // claim has no such excuse: every English word is "distinctive", so two common words ("design", "two") would back anything.
        // A paraphrase of English text shares only some of its words, so an English claim needs several of them, not a half.
        if (CheckpointSummaryPrompt.IsMostlyEnglish(claim)) return supported >= EnglishMinimumShared && supported >= EnglishMinimumShare * words.Length;
        return words.Count(word => IsDistinctive(word) && _words.Contains(word)) >= MinimumDistinctiveTerms;
    }

    /// <summary>
    /// Why a sentence is not supported, as a short code that quotes nothing from the file or the model (a log line must not
    /// carry document text): which check failed first, and how many of its words the file backs.
    /// </summary>
    public string Diagnose(string sentence)
    {
        if (MisusesMarketSize(sentence)) return "market-size-as-goal";
        if (QuotesSlogan(sentence)) return "quoted-slogan";
        if (InventsCurrency(sentence)) return "invented-currency";
        if (ReadsMoneyAsCount(sentence)) return "money-read-as-count";
        var claim = Label().Replace(sentence, string.Empty, 1);
        var words = Words(claim).Where(word => !FunctionWords.Contains(word)).Distinct(StringComparer.Ordinal).ToArray();
        if (words.Length == 0) return "no-content-words";
        if (words.Any(word => word.Any(char.IsDigit) && !_words.Contains(word))) return "invented-number";
        if (InventsRange(claim)) return "invented-range";
        _percentages ??= _sentences.SelectMany(PercentageValues).ToHashSet(StringComparer.Ordinal);
        if (PercentageValues(claim).Any(percentage => !_percentages.Contains(percentage))) return "invented-percentage";
        var supported = words.Count(_words.Contains);
        var distinctive = words.Count(word => IsDistinctive(word) && _words.Contains(word));
        var english = CheckpointSummaryPrompt.IsMostlyEnglish(claim);
        if (supported < _minimumWordSupport * words.Length &&
            (english ? supported < EnglishMinimumShared || supported < EnglishMinimumShare * words.Length : distinctive < MinimumDistinctiveTerms))
            return $"low-word-support {supported}/{words.Length}";
        if (FindOverviewEvidence(sentence).Count > 0) return "supported";
        var terms = words.ToHashSet(StringComparer.Ordinal);
        var best = _sentences.Select(source => Words(source).Distinct(StringComparer.Ordinal).Count(terms.Contains)).DefaultIfEmpty(0).Max();
        return $"no-single-passage best-overlap {best}/{terms.Count}";
    }

    /// <summary>The reasons each sentence of an overview is rejected, without its text.</summary>
    public IReadOnlyList<string> DiagnoseOverview(string overview) =>
        OverviewSentenceList(overview).Select(Diagnose).ToArray();

    /// <summary>The sentences of an overview, in order.</summary>
    public static string[] OverviewSentenceList(string overview) => SentenceEnd().Split(overview.Replace("\n", " ").Trim());

    /// <summary>
    /// The sentences of the overview that the file backs, with their position among all the sentences (so a translation written
    /// sentence by sentence can be matched). Every sentence needs support, the opening claim included. A rejected sentence is
    /// never restored.
    /// </summary>
    public IReadOnlyList<(int Position, string Sentence)> KeptOverviewSentences(string overview, Func<string, bool>? accept = null) =>
        OverviewSentenceList(overview).Select((sentence, position) => (Position: position, Sentence: sentence))
            .Where(item => FindOverviewEvidence(item.Sentence).Count > 0 && (accept is null || accept(item.Sentence)))
            .DistinctBy(item => item.Sentence, StringComparer.Ordinal).Take(OverviewSentences).ToArray();

    /// <summary>Every sentence, including the opening claim, needs support. Never restore a rejected sentence.</summary>
    public string GroundedOverview(string overview, Func<string, bool>? accept = null) =>
        string.Join(' ', KeptOverviewSentences(overview, accept).Select(item => item.Sentence));

    /// <summary>A bounded original passage supports each claim independently, rather than words scattered across the file.</summary>
    public string? FindEvidence(string claim)
    {
        if (_evidence.TryGetValue(claim, out var existing)) return existing;
        if (!Supports(claim)) return _evidence[claim] = null;
        var terms = Words(claim).Where(word => !FunctionWords.Contains(word)).ToHashSet(StringComparer.Ordinal);
        var candidates = _sentences.Where(sentence => sentence.Length <= 300 && !CheckpointSummaryText.IsHeading(sentence))
            .Select(sentence => (Text: sentence, Overlap: Words(sentence).Distinct(StringComparer.Ordinal).Count(terms.Contains)))
            .Where(candidate => candidate.Overlap > 0)
            .OrderByDescending(candidate => candidate.Overlap).Take(12);
        var match = candidates.FirstOrDefault(candidate => new CheckpointSummaryGrounding(candidate.Text, _littleText).Supports(claim));
        return _evidence[claim] = match.Text;
    }

    private const int OverviewSentences = 3;
    private const int EnglishMinimumShared = 3;
    private const double EnglishMinimumShare = 0.2;

    private readonly Dictionary<string, IReadOnlyList<string>> _overviewEvidence = new(StringComparer.Ordinal);

    private const int OverviewPassages = 3;
    private const int MinimumNewTerms = 2;
    private const double CoveredShare = 0.5;
    private const int WindowLines = 3;
    private const int WindowCharacters = 450;

    /// <summary>
    /// The passages that back one sentence of the overview. A sentence that one passage backs is cited by that passage. An overview
    /// sentence often joins several lines of a slide (the idea, the problem, the plan), and the summary may be in another language than
    /// the slide, so no single line holds it: then up to three passages (a line, or two or three neighbouring lines) are chosen so
    /// that together they hold at least half of the claim's words that the file has, each adding new ones. A claim with a figure is
    /// never backed this way: a figure of one line would be joined to the subject of another. Each passage is cited on its own.
    /// </summary>
    public IReadOnlyList<string> FindOverviewEvidence(string claim)
    {
        if (_overviewEvidence.TryGetValue(claim, out var existing)) return existing;
        var single = FindEvidence(claim);
        if (single is not null) return _overviewEvidence[claim] = [single];
        if (!Supports(claim) || Words(claim).Any(IsFigure)) return _overviewEvidence[claim] = [];
        var wanted = Words(claim).Where(word => !FunctionWords.Contains(word) && _words.Contains(word)).ToHashSet(StringComparer.Ordinal);
        if (wanted.Count == 0) return _overviewEvidence[claim] = [];
        var passages = _sentences.Where(sentence => sentence.Length <= WindowCharacters).Concat(Windows())
            .Select(text => (Text: text, Words: Words(text).ToHashSet(StringComparer.Ordinal))).ToArray();
        var uncovered = wanted.ToHashSet(StringComparer.Ordinal);
        var chosen = new List<string>();
        while (chosen.Count < OverviewPassages)
        {
            var next = passages.Select(passage => (passage.Text, passage.Words, Gain: passage.Words.Count(uncovered.Contains)))
                .Where(passage => passage.Gain >= MinimumNewTerms)
                .OrderByDescending(passage => passage.Gain).ThenBy(passage => passage.Text.Length)
                .FirstOrDefault();
            if (next.Text is null) break;
            chosen.Add(next.Text);
            uncovered.ExceptWith(next.Words);
        }
        var covered = wanted.Count - uncovered.Count;
        return _overviewEvidence[claim] = chosen.Count > 0 && covered >= CoveredShare * wanted.Count ? chosen : [];
    }

    private static bool IsFigure(string word) => word.Any(char.IsDigit) && word.All(character => char.IsDigit(character) || character is '%' or '.' or ',');

    /// <summary>Two or three neighbouring lines, joined as the quotes of the screen join them, short enough to read as one passage.</summary>
    private IEnumerable<string> Windows()
    {
        for (var length = 2; length <= WindowLines; length++)
            for (var start = 0; start + length <= _sentences.Count; start++)
            {
                var joined = string.Join(" … ", _sentences.Skip(start).Take(length));
                if (joined.Length <= WindowCharacters) yield return joined;
            }
    }

    /// <summary>
    /// True when the sentence presents a market size as the team's goal: "attract 2 million students in year one"
    /// where the file gives 2 million only as the serviceable market. Small models make this mistake often; it also
    /// spreads into the questions they suggest. Only amounts with a scale (million, triệu…) are checked.
    /// </summary>
    public bool MisusesMarketSize(string sentence)
    {
        if (!TargetWords().IsMatch(sentence)) return false;
        foreach (Match amount in Amount().Matches(sentence))
        {
            var key = Key(amount);
            var stated = _sentences.Where(source => Amount().Matches(source).Any(match => Key(match) == key)).ToArray();
            if (stated.Length > 0 && stated.All(source => MarketSizeWords().IsMatch(source))) return true;
        }
        return false;
    }

    /// <summary>
    /// True when the sentence gives an amount in dollars that the file gives without a currency, or in another one: a
    /// summary turned "62,7% có ngân sách từ 2,5 triệu trở lên" into "từ 2.5 triệu USD". The same number written with
    /// "đồng" or "VND" is not flagged, since a Vietnamese file means dong when it names no currency.
    /// </summary>
    public bool InventsCurrency(string sentence)
    {
        foreach (Match amount in DollarAmount().Matches(sentence))
        {
            var number = AmountNumber(amount.Groups["number"].Value);
            var inSource = _sentences.Where(source => AnyAmount().Matches(source).Any(match => AmountNumber(match.Groups["number"].Value) == number)).ToArray();
            if (inSource.Length > 0 && !inSource.Any(source => DollarAmount().Matches(source).Any(match => AmountNumber(match.Groups["number"].Value) == number)))
                return true;
        }
        return false;
    }

    /// <summary>
    /// True when the sentence counts people or places with a number the file gives only as an amount of money: a plan of
    /// "gói 99.000 đồng mỗi tháng" turned into "mở rộng sang 99.000 người". A number the file also gives for that kind of
    /// thing ("99.000 người dùng") is fine.
    /// </summary>
    public bool ReadsMoneyAsCount(string sentence)
    {
        foreach (Match claimed in NumberWithUnit().Matches(sentence))
        {
            if (!CountedUnits.Contains(claimed.Groups["unit"].Value.ToLowerInvariant())) continue;
            var number = claimed.Groups["number"].Value;
            var units = _sentences.SelectMany(source => NumberWithUnit().Matches(source).Cast<Match>())
                .Where(match => match.Groups["number"].Value == number)
                .Select(match => match.Groups["unit"].Value.ToLowerInvariant()).ToArray();
            if (units.Length > 0 && !units.Any(CountedUnits.Contains) && units.Any(MoneyUnits.Contains)) return true;
        }
        return false;
    }

    /// <summary>
    /// True when a question rests on something the file does not say: a figure it never gives, or a contrast with other
    /// companies it never makes ("why is 11–30 minutes highest while other firms need only minutes?"). A question about
    /// what the file lacks is fine; one that invents the premise sends the reviewer asking about something that is not there.
    /// </summary>
    public bool AssumesUnseenFact(string question) =>
        Words(question).Any(word => word.Any(char.IsDigit) && !_words.Contains(word)) || UnseenContrast().IsMatch(question) ||
        DeniesWhatTheFileStates(question);

    // Things a question may wrongly say the team lacks: what the question says in Vietnamese (accent-free), and how the file
    // may state the same thing in either language.
    private static readonly (Regex Asked, Regex Stated)[] DeniableThings =
    [
        (Plain("danh gia|xep hang|uy tin"), Plain("reviews?\b|ratings?\b|reputation|danh gia|xep hang|uy tin")),
        (Plain("xac minh|kiem duyet"), Plain("verif|moderat|xac minh|kiem duyet")),
        (Plain("thanh toan"), Plain("payments?\b|escrow|thanh toan")),
        (Plain("tro chuyen|nhan tin|chat\b"), Plain("chat\b|messag|nhan tin|tro chuyen")),
        (Plain("bao mat|an toan"), Plain("secur|safe|bao mat|an toan"))
    ];

    private static Regex Plain(string terms) => new("(?<![a-z0-9])(?:" + terms + ")", RegexOptions.CultureInvariant);

    private static readonly Regex Denial = new(@"(?<![a-z0-9])(?:chua|khong|thieu)(?: co| duoc)?(?: he thong| co che| quy trinh| ke hoach)?\s*$", RegexOptions.CultureInvariant);

    /// <summary>
    /// True when a question says the team has no X ("trong môi trường chưa có hệ thống đánh giá") although the file states X
    /// ("two-way ratings and reviews"): the question would send the reviewer asking about something that is there.
    /// </summary>
    private bool DeniesWhatTheFileStates(string question)
    {
        var plain = MentorEvidenceText.Normalize(question);
        foreach (var (asked, stated) in DeniableThings)
            foreach (Match thing in asked.Matches(plain))
                if (Denial.IsMatch(plain[..thing.Index].TrimEnd()) && stated.IsMatch(_plain)) return true;
        return false;
    }

    /// <summary>True when the sentence gives a range ("200-1.000") whose two ends the file never states next to each other.</summary>
    private bool InventsRange(string sentence) =>
        Range().Matches(sentence).Any(range => !_text.Contains(" " + CheckpointSummaryText.Normalize(range.Value) + " ", StringComparison.Ordinal));

    private static string AmountNumber(string value) => value.Replace(',', '.').TrimEnd('.');

    private static IEnumerable<string> PercentageValues(string text) =>
        Percentage().Matches(text).Select(match => match.Groups["number"].Value.Replace(',', '.').Replace('−', '-'));

    [GeneratedRegex(@"(?<![\p{L}\d.,])(?<number>[−-]?\d+(?:[.,]\d+)?)\s*(?:%|phần\s+trăm\b|percent\b|per\s+cent\b)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Percentage();

    /// <summary>
    /// True when a figure of the sentence ("5 lỗi") appears in the file only inside quotation marks: a post title or a
    /// slogan such as “5 lỗi formatting sinh viên gặp mỗi tuần” in a content plan, not a finding of the team.
    /// </summary>
    public bool QuotesSlogan(string sentence)
    {
        var tokens = Words(sentence).ToArray();
        for (var i = 0; i + 1 < tokens.Length; i++)
        {
            if (!tokens[i].Any(char.IsDigit) || !tokens[i + 1].Any(char.IsLetter)) continue;
            var phrase = $" {tokens[i]} {tokens[i + 1]} ";
            var stated = _sentences.Where(source => $" {CheckpointSummaryText.Normalize(source)} ".Contains(phrase, StringComparison.Ordinal))
                .ToArray();
            if (stated.Length > 0 && stated.All(source => OpeningQuote().IsMatch(source))) return true;
        }
        return false;
    }

    private static string Key(Match amount)
    {
        var scale = amount.Groups["scale"].Value.ToLowerInvariant() switch
        {
            "triệu" or "million" => "m",
            "tỷ" or "billion" => "b",
            _ => "k"
        };
        return amount.Groups["number"].Value.Replace(',', '.') + scale;
    }

    private static bool IsDistinctive(string word) =>
        word.Any(char.IsDigit) || (word.Length >= 3 && word.All(character => character is >= 'a' and <= 'z'));

    private static IEnumerable<string> Words(string text) =>
        CheckpointSummaryText.Normalize(Percentage().Replace(text, "${number}%"))
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

    // "3.2" or "E-HUB.com.vn" do not end a sentence: only a mark followed by a space does.
    [GeneratedRegex(@"(?<=[.!?])\s+")]
    private static partial Regex SentenceEnd();

    [GeneratedRegex(@"^\s*\p{L}[\p{L} ]{1,30}:\s+")]
    private static partial Regex Label();

    [GeneratedRegex(@"(?<number>\d+(?:[.,]\d+)*)\s*(?<unit>\p{L}+)", RegexOptions.CultureInvariant)]
    private static partial Regex NumberWithUnit();

    [GeneratedRegex(@"\d[\d.,]*\s*[-–—]\s*\d[\d.,]*")]
    private static partial Regex Range();

    [GeneratedRegex(@"trong khi (?:các|những|nhiều)?[^?]{0,40}\bkhác\b|\b(?:while|whereas) (?:other|most|many)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UnseenContrast();

    [GeneratedRegex("^\\s*[\"“‘]")]
    private static partial Regex OpeningQuote();

    [GeneratedRegex(@"(?<number>\d+(?:[.,]\d+)*)\s*(?:triệu|tỷ|nghìn|ngàn|million|billion|thousand|k)?\s*(?:usd|\$|đô la|đô)(?![\p{L}])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DollarAmount();

    [GeneratedRegex(@"(?<number>\d+(?:[.,]\d+)*)\s*(?:triệu|tỷ|nghìn|ngàn|million|billion|thousand|k|usd|\$|đô la|đô|đồng|vnd|vnđ|đ)?(?![\p{L}\d])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AnyAmount();

    [GeneratedRegex(@"(?<number>\d+(?:[.,]\d+)?)\s*(?<scale>triệu|million|tỷ|billion|nghìn|ngàn|thousand|k)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Amount();

    [GeneratedRegex(@"\b(?:tam|sam|market size|potential users?|potential customers?|estimated|quy mô thị trường|thị trường tiềm năng|khách hàng tiềm năng|người dùng tiềm năng)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MarketSizeWords();

    [GeneratedRegex(@"mục tiêu|thu hút|đạt được|\btarget|\bacquire|\breach|năm đầu|trong năm|year one|first year",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TargetWords();
}
