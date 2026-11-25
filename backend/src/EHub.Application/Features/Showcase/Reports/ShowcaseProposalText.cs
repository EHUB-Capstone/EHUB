using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EHub.Application.Features.Showcase.Reports;

public static partial class ShowcaseProposalText
{
    // Proposal selection is literal: retain Vietnamese accents and đ, and never select a fragment of a word.
    public static string Canonical(string value)
    {
        var visible = new string(value.Normalize(NormalizationForm.FormC).ToLowerInvariant()
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.Format).ToArray());
        return string.Join(" ", Words().Matches(visible).Select(x => x.Value));
    }

    public static bool IsInComment(string comment, string phrase)
    {
        var candidate = Canonical(phrase);
        return candidate.Any(char.IsLetter)
            && (" " + Canonical(comment) + " ").Contains(" " + candidate + " ", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"[\p{L}\p{M}\p{Nd}]+", RegexOptions.CultureInvariant, 100)]
    private static partial Regex Words();
}
