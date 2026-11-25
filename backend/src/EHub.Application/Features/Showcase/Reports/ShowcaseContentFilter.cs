using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EHub.Domain.Entities;
using EHub.Domain.Enums;

namespace EHub.Application.Features.Showcase.Reports;

// Deterministic phrase matching for submission validation; no external AI calls.
public static partial class ShowcaseContentFilter
{
    public static string Normalize(string value) => Normalize(value, false);
    public static string NormalizeComment(string value) => Normalize(value, true);

    private static string Normalize(string value, bool preserveAccents)
    {
        var output = new StringBuilder();
        foreach (var character in value.ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.Format) continue;
            if (category == UnicodeCategory.NonSpacingMark)
            {
                if (preserveAccents) output.Append(character);
                continue;
            }
            var c = character switch { 'đ' => 'd', '0' => 'o', '1' => 'i', '3' => 'e', '4' or '@' => 'a', '5' or '$' => 's', '7' => 't', _ => character };
            c = char.IsLetterOrDigit(c) ? c : ' ';
            // Collapse repeated letters and spacing in both the rule and the input.
            if (output.Length == 0 || output[^1] != c) output.Append(c);
        }
        return output.ToString().Normalize(NormalizationForm.FormC).Trim();
    }

    public static string Hash(string content) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    internal static string PageText(LatestProject project, ShowcaseProject showcase) =>
        JsonSerializer.Serialize(new { project.ProjectName, project.Description, project.Problem, project.Solution, project.TargetUsers,
            showcase.Highlights, Links = showcase.Links.OrderBy(x => x.SortOrder).ThenBy(x => x.Id).Select(x => new { x.Label, x.Url }) });

    public static IReadOnlyList<ContentFinding> Evaluate(IEnumerable<string> fields, IReadOnlyCollection<ShowcaseModerationRule> rules, bool isComment, bool allMatches = false)
    {
        var values = fields.ToArray();
        var normalized = values.Select(value => " " + Normalize(value) + " ").ToArray();
        var exact = values.Select(value => Normalize(value, true).Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToArray();
        var results = new List<ContentFinding>();
        foreach (var rule in rules.Where(x => x.IsEnabled).OrderBy(x => x.Id))
        {
            // Rules may be a single word ("ngu") or a phrase. Match complete words at any position,
            // including the start/end of a field; never split a phrase into unrelated blocked words.
            var phrase = Normalize(rule.Phrase);
            if (phrase.Any(char.IsLetter) && normalized.Where((value, index) => value.Contains(" " + phrase + " ", StringComparison.Ordinal)
                    && ContainsPhrase(exact[index], Normalize(rule.Phrase, true).Split(' ', StringSplitOptions.RemoveEmptyEntries))).Any())
                results.Add(new ContentFinding(rule.Reason, $"Phrase / Cụm từ: {rule.Phrase}. {rule.Description}", rule.Category));
            if (!allMatches && results.Count >= 5) break;
        }
        // Links are normal in a product showcase. This signal only applies to comments.
        if (isComment && values.Sum(value => UrlPattern().Matches(value).Count) >= 4)
            results.Add(new ContentFinding(ShowcaseReportReason.Spam, "Multiple links in one comment / Nhiều đường link trong một bình luận.", ShowcaseViolationCategory.Spam));
        return results;
    }

    private static bool ContainsPhrase(string[] words, string[] phrase)
    {
        // Folding accents on a standalone word can turn ordinary Vietnamese into profanity
        // ("lon" -> "lồn", "cac" -> "cặc"). Keep accents for these single-word rules.
        var requiresAccents = phrase.Length == 1 && HasAccent(phrase[0]);
        for (var start = 0; start <= words.Length - phrase.Length; start++)
        {
            var matches = true;
            for (var i = 0; i < phrase.Length; i++)
            {
                var word = words[start + i];
                // An explicitly accented word must keep its meaning: "ngủ" must not become "ngu".
                // Accent-free user spelling can still match accented multi-word rules.
                if (requiresAccents || HasAccent(word)
                    ? word != phrase[i]
                    : Normalize(word) != Normalize(phrase[i])) { matches = false; break; }
            }
            if (matches) return true;
        }
        return false;
    }

    private static bool HasAccent(string word) => word.Normalize(NormalizationForm.FormD)
        .Any(c => CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark);

    [GeneratedRegex(@"(?:https?://|www\.)[^\s]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 100)]
    private static partial Regex UrlPattern();
}

public sealed record ContentFinding(ShowcaseReportReason Reason, string Evidence, ShowcaseViolationCategory Category = ShowcaseViolationCategory.Insult);
