using System.Text.RegularExpressions;

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
    private readonly double _minimumWordSupport;

    /// <param name="littleText">The file is mostly images: key points need more of their words in the text that was read.</param>
    public CheckpointSummaryGrounding(string sourceText, bool littleText = false)
    {
        _words = Words(sourceText).ToHashSet(StringComparer.Ordinal);
        _sentences = CheckpointSummaryText.Sentences(sourceText);
        _text = " " + CheckpointSummaryText.Normalize(sourceText) + " ";
        _minimumWordSupport = littleText ? MinimumWordSupportLittleText : MinimumWordSupport;
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
        var supported = words.Count(_words.Contains);
        if (supported >= _minimumWordSupport * words.Length) return true;
        // Slides are often in English while the summary is in Vietnamese: then distinctive terms carry the evidence.
        return words.Count(word => IsDistinctive(word) && _words.Contains(word)) >= MinimumDistinctiveTerms;
    }

    /// <summary>Keeps the first sentence (the model's statement of what the file is) and every supported one after it.</summary>
    public string GroundedOverview(string overview)
    {
        var sentences = SentenceEnd().Split(overview.Replace('\n', ' ').Trim());
        var kept = sentences.Where((sentence, index) => index == 0 ? !MisusesMarketSize(sentence) : Supports(sentence)).ToArray();
        return kept.Length > 0 ? string.Join(' ', kept) : sentences[0];
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
        Words(question).Any(word => word.Any(char.IsDigit) && !_words.Contains(word)) || UnseenContrast().IsMatch(question);

    /// <summary>True when the sentence gives a range ("200-1.000") whose two ends the file never states next to each other.</summary>
    private bool InventsRange(string sentence) =>
        Range().Matches(sentence).Any(range => !_text.Contains(" " + CheckpointSummaryText.Normalize(range.Value) + " ", StringComparison.Ordinal));

    private static string AmountNumber(string value) => value.Replace(',', '.').TrimEnd('.');

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
        CheckpointSummaryText.Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);

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
