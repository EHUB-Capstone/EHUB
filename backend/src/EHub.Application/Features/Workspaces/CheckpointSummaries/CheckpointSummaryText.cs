using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

/// <summary>Pure text helpers: contact redaction before text reaches the model, sentence selection and quotes.</summary>
internal static partial class CheckpointSummaryText
{
    /// <summary>
    /// Removes characters PostgreSQL's jsonb refuses, so text taken from files can be stored in a summary: NUL and other
    /// control characters (PDF fonts often produce them) and unpaired surrogates. Line breaks and tabs are kept.
    /// </summary>
    public static string Clean(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var builder = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i];
            if (char.IsHighSurrogate(character) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                builder.Append(character).Append(text[++i]);
                continue;
            }
            if (char.IsSurrogate(character)) continue;
            if (char.IsControl(character) && character is not ('\n' or '\r' or '\t')) continue;
            builder.Append(character);
        }
        return builder.ToString();
    }

    public static string Redact(string text)
    {
        text = Email().Replace(text, "[email]");
        text = Phone().Replace(text, "[số điện thoại]");
        return StudentId().Replace(text, "[MSSV]");
    }

    // Vietnamese and English: many pitch decks are written in English, and without English terms their problem and
    // solution slides scored nothing and were left out of the prompt in favour of financial tables.
    private static readonly string[] Keywords =
    [
        "khách hàng", "doanh thu", "giá", "chi phí", "đối thủ", "rủi ro", "vốn", "mvp", "prototype", "khảo sát",
        "phỏng vấn", "kết quả", "mục tiêu", "giải pháp", "vấn đề", "kênh", "marketing", "lợi nhuận", "thị trường",
        "người dùng", "kế hoạch", "tài chính", "hòa vốn", "đơn", "tỷ lệ",
        "problem", "pain", "customer", "user", "solution", "feature", "market", "competitor", "revenue", "pricing",
        "price", "cost", "survey", "interview", "traction", "retention", "conversion", "margin", "funding", "risk",
        "validation", "target", "segment", "value"
    ];

    private const int TitleWords = 6;
    private const int MinimumCoverageWords = 4;

    /// <summary>Lines, with long lines split into sentences. Headings and slide bullets stay whole.</summary>
    public static IReadOnlyList<string> Sentences(string text)
    {
        var result = new List<string>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.Length <= 200) { result.Add(line); continue; }
            result.AddRange(SentenceEnd().Split(line).Select(item => item.Trim()).Where(item => item.Length > 0));
        }
        return result;
    }

    /// <summary>
    /// Shrinks a long document to about <paramref name="budget"/> characters without a model, so a single model call
    /// can read it. Sentences with numbers, requirement phrases, domain keywords, headings and the opening of the
    /// document score higher; near-duplicates (repeated logs) are pushed down; the original order is kept.
    /// Every section (text between two titles, which on slides is roughly one slide) first gets its best sentence, so
    /// one figure-heavy part such as a financial plan can no longer push the problem and solution out of the prompt.
    /// On the benchmark this replaced several chunk-summary calls and cut a 20,000-character report from 506 s to 128 s.
    /// </summary>
    /// <param name="requirementTitles">Checklist titles and descriptions; their word pairs mark relevant sentences.</param>
    public static string Select(string text, IReadOnlyCollection<string> requirementTitles, int budget)
    {
        if (text.Length <= budget) return text;
        var sentences = Sentences(text);
        var scored = Score(sentences, requirementTitles);
        var chosen = new HashSet<int>();
        var used = 0;
        void Add((double Score, int Position, string Text) item)
        {
            if (chosen.Contains(item.Position) || used + item.Text.Length + 1 > budget) return;
            chosen.Add(item.Position);
            used += item.Text.Length + 1;
        }

        var section = 0;
        var sectionOf = new int[sentences.Count];
        for (var i = 0; i < sentences.Count; i++)
        {
            if (IsTitle(sentences[i])) section++;
            sectionOf[i] = section;
        }
        var coverage = scored
            .Where(item => item.Score > 0 && !IsTitle(item.Text) &&
                item.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= MinimumCoverageWords)
            .GroupBy(item => sectionOf[item.Position])
            .Select(group => group.OrderByDescending(item => item.Score).ThenBy(item => item.Position).First())
            .OrderByDescending(item => item.Score).ThenBy(item => item.Position);
        foreach (var item in coverage) Add(item);
        foreach (var item in scored.OrderByDescending(item => item.Score).ThenBy(item => item.Position)) Add(item);
        return string.Join('\n', scored.Where(item => chosen.Contains(item.Position)).Select(item => item.Text));
    }

    /// <summary>A slide or section title: a few words in capitals, or a label ending in a colon ("Goal:", "Point 1:").</summary>
    internal static bool IsTitle(string sentence)
    {
        var trimmed = sentence.Trim();
        if (trimmed.Length == 0 || trimmed.EndsWith('.')) return false;
        if (trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > TitleWords) return false;
        var letters = trimmed.Where(char.IsLetter).ToArray();
        return trimmed.EndsWith(':') || (letters.Length >= 3 && letters.All(char.IsUpper));
    }

    /// <summary>A table row such as "Total Users 2,000 4,000 6,000": figures without the words that explain them.</summary>
    private static bool IsNumberRow(string sentence) =>
        sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries).Count(word => word.Count(char.IsLetter) >= 3) <= 2;

    /// <summary>
    /// A file's most informative full sentences (not headings), quoted as written, in document order. Chosen without the
    /// model, so every file in a checkpoint gets its own preview at no extra model time.
    /// </summary>
    public static IReadOnlyList<string> Highlights(string text, IReadOnlyCollection<string> requirementTitles, int count)
    {
        var sentences = Sentences(text);
        var scored = Score(sentences, requirementTitles);
        var contents = ContentsLines(sentences);
        return scored
            .Where(item => item.Text.Length is >= 30 and <= 300 && !IsHeading(item.Text) && !contents[item.Position])
            .OrderByDescending(item => item.Score + QuoteAdjustment(item.Text, item.Position, sentences)).ThenBy(item => item.Position)
            .Take(count)
            .OrderBy(item => item.Position)
            .Select(item => item.Text)
            .ToArray();
    }

    private static List<(double Score, int Position, string Text)> Score(IReadOnlyList<string> sentences, IReadOnlyCollection<string> requirementTitles)
    {
        var requirementPairs = new HashSet<string>(requirementTitles.SelectMany(WordPairs), StringComparer.Ordinal);
        var seen = new List<HashSet<string>>();
        var scored = new List<(double Score, int Position, string Text)>(sentences.Count);
        for (var position = 0; position < sentences.Count; position++)
        {
            var sentence = sentences[position];
            var lowered = sentence.ToLower(CultureInfo.InvariantCulture);
            var score = 0.0;
            // A figure counts most inside a sentence that says what it measures.
            if (sentence.Any(char.IsDigit)) score += IsNumberRow(sentence) ? 1 : 3;
            if (requirementPairs.Overlaps(WordPairs(sentence))) score += 3;
            score += Math.Min(3, Keywords.Count(keyword => lowered.Contains(keyword, StringComparison.Ordinal)));
            if (IsHeading(sentence)) score += 1.5;
            if (position < sentences.Count * 0.1) score += 1;
            var signature = Trigrams(Normalize(sentence));
            if (signature.Count > 0 && seen.Any(other => signature.Count(other.Contains) > 0.6 * signature.Count)) score -= 4;
            seen.Add(signature);
            scored.Add((score, position, sentence));
        }
        return scored;
    }


    /// <summary>
    /// A quote shown to a reviewer should read as a sentence. Text in capitals (slide banners, member lists), contact
    /// placeholders and the cover page rarely say anything about the work; a sentence with several figures usually does.
    /// </summary>
    private static double QuoteAdjustment(string sentence, int position, IReadOnlyList<string> sentences)
    {
        var total = sentences.Count;
        var adjustment = 0.0;
        var letters = sentence.Where(char.IsLetter).ToArray();
        if (letters.Length > 0 && letters.Count(char.IsUpper) >= 0.7 * letters.Length) adjustment -= 4;
        if (sentence.Contains("[MSSV]", StringComparison.Ordinal) || sentence.Contains("[email]", StringComparison.Ordinal) ||
            sentence.Contains("[số điện thoại]", StringComparison.Ordinal)) adjustment -= 4;
        // Score() favours the opening of a document for the model's prompt; for quotes the cover page is noise.
        if (position < total * 0.1) adjustment -= 1;
        // Dashboard exports are full of emoji and instructions ("Hướng dẫn tương tác: chọn ... tại menu bên trái").
        if (sentence.Any(character => char.IsSurrogate(character) || character is >= '\u2600' and <= '\u27BF')) adjustment -= 3;
        if (InterfaceText().IsMatch(sentence)) adjustment -= 3;
        // A chart's labels read as one line, or one topic of a numbered list ("2. Quy trình quản lý…" between "1." and "3."):
        // they name things without saying what was found.
        if (IsLabelStrip(sentence) || IsListTopic(sentences, position)) adjustment -= 4;
        // "7 năm kinh nghiệm thực chiến" describes a person (an interviewee's profile), not the team's work.
        if (ExperienceYears().IsMatch(sentence)) adjustment -= 4;
        // "Thời gian thực hiện: Từ 24/09 đến 04/10." and "Thang đo chuẩn hóa: Likert 5 mức" describe the method.
        if (IsMethodology(sentence)) adjustment -= 3;
        // A question is a survey item or a prompt ("Nếu không quản lý tốt chứng từ, rủi ro nào có thể phát sinh?"), not a finding.
        if (sentence.EndsWith('?')) adjustment -= 2;
        else if (sentence.EndsWith('.') || sentence.EndsWith('!')) adjustment += 1;
        if (Figures().Matches(sentence).Count >= 2) adjustment += 1;
        return adjustment;
    }

    /// <summary>A line that describes how the work was done (scale, method, sample, period), not what it found.</summary>
    internal static bool IsMethodology(string sentence) => MethodologyStart().IsMatch(sentence);

    private const double CapitalsShare = 0.8;
    private const int MinimumCapitalLetters = 12;
    private const int FragmentWords = 3;

    /// <summary>
    /// A long run of capitals: chart labels glued together ("TỔNG SỐ PHẢN HỒI SẴN SÀNG DÙNG THỬ…") or a report title.
    /// It names things but states nothing, so it is a poor quote for a criterion.
    /// </summary>
    internal static bool IsMostlyCapitals(string sentence)
    {
        var letters = sentence.Where(char.IsLetter).ToArray();
        return letters.Length >= MinimumCapitalLetters && letters.Count(char.IsUpper) >= CapitalsShare * letters.Length;
    }

    private const int LabelStripFigures = 3;
    private const int NumberedLabelWords = 16;

    /// <summary>
    /// A chart's labels read as one line: figures each followed by a capitalised label ("52 Doanh nghiệp tham gia 98% Doanh
    /// nghiệp khảo sát 82,7% Mất nhiều thời gian…"). It lists numbers without saying what they show.
    /// </summary>
    internal static bool IsLabelStrip(string sentence) => FigureThenLabel().Matches(sentence).Count >= LabelStripFigures;

    /// <summary>
    /// An item of a numbered list that names a topic without a figure ("7. Vấn đề mà công cụ hiện tại chưa xử lý được."): the
    /// interview or survey topics of a slide. It is a heading for what follows, not a finding.
    /// </summary>
    internal static bool IsNumberedLabel(string sentence)
    {
        var trimmed = sentence.Trim();
        return NumberedLabel().IsMatch(trimmed) && trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= NumberedLabelWords;
    }

    /// <summary>A numbered line whose neighbour carries the previous or next number: an agenda or the topics of an interview.</summary>
    private static bool IsListTopic(IReadOnlyList<string> sentences, int position)
    {
        var own = ListNumber(sentences[position]);
        if (own < 0 || sentences[position].Contains('%')) return false;
        return (position > 0 && Math.Abs(ListNumber(sentences[position - 1]) - own) == 1) ||
            (position + 1 < sentences.Count && Math.Abs(ListNumber(sentences[position + 1]) - own) == 1);
    }

    private static int ListNumber(string sentence) =>
        LeadingNumber().Match(sentence) is { Success: true } match ? int.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture) : -100;

    private const int ThinWords = 5;

    /// <summary>
    /// Too little to count as the content of a heading: a table cell or a chart's answer option ("Dưới 15 nhân", "Segment Quy
    /// mô/giá trị"). A short bullet that ends like a sentence ("Giảm chi phí vận hành.") is content. Stricter than
    /// <see cref="IsFragment"/>, which only decides what is not worth appending to a quote.
    /// </summary>
    internal static bool IsThin(string sentence)
    {
        var words = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return words < FragmentWords || (words < ThinWords && !sentence.TrimEnd().EndsWith('.') && !sentence.TrimEnd().EndsWith('!'));
    }

    /// <summary>Instructions of a dashboard export ("🎯 CHỌN LOẠI HÌNH… Hướng dẫn tương tác: chọn tại menu thả xuống"), not content.</summary>
    internal static bool IsInterfaceText(string sentence) =>
        InterfaceText().IsMatch(sentence) || sentence.Any(character => char.IsSurrogate(character) || character is >= '☀' and <= '➿');

    /// <summary>
    /// Accent-free text that promises or says it is still being written ("sẽ hoàn thiện … sau", "đang hoàn thiện nội dung"):
    /// the thing it names is not there yet, so it is no evidence of it.
    /// </summary>
    internal static bool IsDeferral(string normalised) => Deferral().IsMatch(normalised);

    /// <summary>
    /// A question for the team, or null when the model wrote something else: a statement about what "the document" lacks
    /// ("Tài liệu chưa nêu rõ…", also wrong when the file does say it), a question about the document, or no question at all.
    /// </summary>
    internal static string? AsTeamQuestion(string question)
    {
        var text = LeadingAccordingTo().Replace(question.Trim(), string.Empty);
        if (text.Length == 0 || !text.EndsWith('?') || AboutTheDocument().IsMatch(text)) return null;
        return char.ToUpperInvariant(text[0]) + text[1..];
    }

    /// <summary>A table cell or a stray letter ("Nhu cầu/JTB", "D"): too short to say anything on its own.</summary>
    internal static bool IsFragment(string sentence) =>
        sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < FragmentWords;

    /// <summary>
    /// Accent-free text that advises what to do ("Strategic implication: nên vào thị trường bằng pilot hẹp",
    /// "đề xuất giải pháp"). It answers "what next", not "what is the problem".
    /// </summary>
    internal static bool IsRecommendation(string normalised) => Recommendation().IsMatch(normalised);

    private const int ContentsMaximumLines = 14;
    private const int ContentsMaximumLineLength = 80;

    /// <summary>
    /// Marks the agenda that follows a "Table of Contents" / "Mục lục" line: short lines up to the first full sentence.
    /// They name sections ("Phỏng vấn chuyên sâu Phân khúc Khách hàng") and must not be quoted as evidence for them.
    /// </summary>
    internal static bool[] ContentsLines(IReadOnlyList<string> sentences)
    {
        var contents = new bool[sentences.Count];
        for (var i = 0; i < sentences.Count; i++)
        {
            if (!ContentsHeading().IsMatch(sentences[i])) continue;
            contents[i] = true;
            for (var next = i + 1; next < sentences.Count && next <= i + ContentsMaximumLines; next++)
            {
                if (sentences[next].Length > ContentsMaximumLineLength || sentences[next].EndsWith('.')) break;
                contents[next] = true;
            }
        }
        return contents;
    }

    internal static bool IsHeading(string sentence) =>
        sentence.Length <= 80 && !sentence.EndsWith('.') && sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 10;

    /// <summary>Lower-case NFC text with punctuation collapsed to single spaces, for tolerant comparisons.</summary>
    public static string Normalize(string text)
    {
        var lowered = text.Normalize(NormalizationForm.FormC).ToLower(CultureInfo.InvariantCulture);
        return Whitespace().Replace(NonWord().Replace(lowered, " "), " ").Trim();
    }

    public static HashSet<string> Trigrams(string normalized)
    {
        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i + 2 < words.Length; i++) result.Add($"{words[i]} {words[i + 1]} {words[i + 2]}");
        return result;
    }

    private static readonly HashSet<string> PairStopWords = new(StringComparer.Ordinal)
    {
        "và", "hoặc", "của", "có", "các", "đã", "sau", "khi",
        "the", "and", "or", "of", "to", "a", "an", "in", "for", "with", "is", "are", "on", "by"
    };

    private static HashSet<string> WordPairs(string text)
    {
        var words = Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i + 1 < words.Length; i++)
            if (!PairStopWords.Contains(words[i]) && !PairStopWords.Contains(words[i + 1])) result.Add($"{words[i]} {words[i + 1]}");
        return result;
    }

    public static string Clip(string? text, int maxLength)
    {
        var trimmed = (text ?? string.Empty).Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength].TrimEnd() + "…";
    }

    [GeneratedRegex(@"[\w.+-]+@[\w-]+(\.[\w-]+)+")]
    private static partial Regex Email();

    [GeneratedRegex(@"(?<!\d)(\+84|0)\d{2,3}[\s.-]?\d{3}[\s.-]?\d{3,4}(?!\d)")]
    private static partial Regex Phone();

    // FPT-style student codes such as SE190001.
    [GeneratedRegex(@"\b[A-Z]{2}\d{6}\b")]
    private static partial Regex StudentId();

    [GeneratedRegex(@"[^\w%]+")]
    private static partial Regex NonWord();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"\d+(?:[.,:]\d+)*")]
    private static partial Regex Figures();

    [GeneratedRegex(@"hướng dẫn|menu|nhấp|bấm vào|click|dropdown|thả xuống|bên trái|bộ lọc", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InterfaceText();

    [GeneratedRegex(@"^\s*(?:table of contents|contents|agenda|mục lục|nội dung(?: chính)?)\s*:?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ContentsHeading();

    [GeneratedRegex(@"^\s*(?:thang đo|phương pháp|thời gian thực hiện|thời gian khảo sát|quy mô mẫu|cỡ mẫu|công cụ|cách thức|methodology|sample size|method)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MethodologyStart();

    [GeneratedRegex(@"(?<![a-z0-9])(?:implications?|recommend\w*|khuyen nghi|de xuat|nen (?:vao|uu tien|trien khai|tap trung|bat dau|chon|thu)|should|next steps?|buoc tiep theo)(?![a-z0-9])",
        RegexOptions.CultureInvariant)]
    private static partial Regex Recommendation();

    [GeneratedRegex(@"(?<![a-z0-9])(?:se|dang|sap) (?:hoan thien|bo sung|cap nhat)(?![a-z0-9]).*(?:\bsau\b|con lai|noi dung)",
        RegexOptions.CultureInvariant)]
    private static partial Regex Deferral();

    [GeneratedRegex(@"^(?:theo|trong) (?:tài liệu|báo cáo)\s*,?\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LeadingAccordingTo();

    [GeneratedRegex(@"(?:tài liệu|báo cáo|bản nộp) (?:này )?(?:có|chưa|không|đã|chỉ)\b|(?:chưa|không) (?:được )?(?:nêu|đề cập|trình bày|ghi|có thông tin)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AboutTheDocument();

    [GeneratedRegex(@"\d(?:[.,/]\d+)*%?\s+\p{Lu}")]
    private static partial Regex FigureThenLabel();

    [GeneratedRegex(@"^\s*(?<number>\d{1,2})[.)]\s*\p{L}")]
    private static partial Regex LeadingNumber();

    [GeneratedRegex(@"^\d{1,2}[.)]\s*\p{L}\D*$")]
    private static partial Regex NumberedLabel();

    [GeneratedRegex(@"\b\d{1,2}\+?\s*(?:năm|years?)\s+(?:kinh nghiệm|of experience|experience)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExperienceYears();

    [GeneratedRegex(@"(?<=[.!?])\s+")]
    private static partial Regex SentenceEnd();
}
