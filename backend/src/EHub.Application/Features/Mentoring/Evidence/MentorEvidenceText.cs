using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EHub.Application.Features.Mentoring.Evidence;

// Bounded, conservative clause filtering shared by CV extraction and ranking.
// This handles explicit exclusions, not general language understanding.
public static class MentorEvidenceText
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(100);
    private const RegexOptions Options = RegexOptions.CultureInvariant;

    public static string Positive(string? text) => Extract(text).Positive;
    public static string Excluded(string? text) => Extract(text).Excluded;

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        string decomposed;
        try { decomposed = value.ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD); }
        catch (ArgumentException)
        {
            var valid = new StringBuilder(value.Length);
            foreach (var rune in value.EnumerateRunes()) valid.Append(rune.ToString());
            decomposed = valid.ToString().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        }
        var output = new StringBuilder(decomposed.Length);
        var pendingSpace = false;
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsWhiteSpace(character)) { pendingSpace = output.Length > 0; continue; }
            if (pendingSpace) output.Append(' ');
            pendingSpace = false;
            output.Append(character);
        }
        return output.ToString();
    }

    private static (string Positive, string Excluded) Extract(string? text)
    {
        // Vietnamese "ai sở hữu" means "who owns", not artificial intelligence.
        // Preserve the uppercase acronym AI and unambiguous technical usage.
        text = Regex.Replace(text ?? "", @"\b(?:ai|Ai)(?=\s+(?:sở hữu|được dùng|được sử dụng|sẽ mua|chịu trả))",
            "người nào", Options, Timeout);
        var normalized = Normalize(text.Replace('\r', ';').Replace('\n', ';'));
        // These constructions affirm both items rather than denying the first.
        normalized = Replace(normalized, @"\b(?:khong chi|khong nhung|not only)\b", " ");
        var clauses = Regex.Split(normalized,
            @"[;\r\n!?]+|\.(?=\s|$)|\b(?:nhung|tuy nhien|but|however|instead|ma can|chi can|va can)\b|,(?=\s*(?:can|need|co kinh nghiem|experienced)\b)",
            Options, Timeout);
        var positive = new List<string>();
        var excluded = new List<string>();
        foreach (var clause in clauses)
        {
            if (Regex.IsMatch(clause,
                @"\b(?:nam ngoai (?:nhu cau|pham vi)|ngoai pham vi|out of scope|not required|not needed|khong (?:can thiet|duoc yeu cau))\b",
                Options, Timeout)) { excluded.Add(clause); continue; }
            var denial = Regex.Match(clause,
                @"\b(?:khong|chua|chang|no|not|without|never|lack|lacks|lacking)\b", Options, Timeout);
            var affirmative = denial.Success ? clause[..denial.Index] : clause;
            if (denial.Success) excluded.Add(clause[denial.Index..]);
            if (!string.IsNullOrWhiteSpace(affirmative)) positive.Add(affirmative.Trim());
        }
        return (string.Join(' ', positive), string.Join(' ', excluded));
    }

    private static string Replace(string text, string pattern, string replacement) =>
        Regex.Replace(text, pattern, replacement, Options, Timeout);
}
