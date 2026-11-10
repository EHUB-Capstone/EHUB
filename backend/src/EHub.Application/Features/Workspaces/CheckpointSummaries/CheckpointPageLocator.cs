using EHub.Application.Common.Interfaces.Services;

namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

/// <summary>
/// Tells on which page or slide a quoted sentence of a file is, so a reviewer can open the file there. The summary reads the
/// text after it was tidied (lines joined, split syllables repaired, titles repeated on every slide dropped), so a quote is
/// found by the start of its first part with spaces and punctuation ignored, not by an exact copy. Only PDF and slides have
/// pages; a Word file has none to give, because its pages depend on how it is laid out when opened.
/// </summary>
internal sealed class CheckpointPageLocator
{
    private const int MinimumCharacters = 14;
    private const int MaximumCharacters = 40;

    private readonly string[] _pages;

    private CheckpointPageLocator(string[] pages) => _pages = pages;

    public static CheckpointPageLocator? From(ExtractedText extracted) =>
        extracted.PageTexts is { Count: > 0 } pages ? new CheckpointPageLocator(pages.Select(Squash).ToArray()) : null;

    /// <returns>The 1-based page of the quote, or null when it cannot be told.</returns>
    public int? Locate(string? quote)
    {
        if (string.IsNullOrWhiteSpace(quote)) return null;
        // A quote may join several lines ("Heading … first line"). The lines are next to each other on the page, so the joined
        // start finds a short heading; when the lines were not contiguous, the first part alone is tried.
        foreach (var candidate in new[] { quote.Replace(" … ", " "), quote.Split(" … ", 2)[0] })
        {
            var key = Squash(candidate);
            if (key.Length < MinimumCharacters) continue;
            key = key[..Math.Min(key.Length, MaximumCharacters)];
            for (var i = 0; i < _pages.Length; i++)
                if (_pages[i].Contains(key, StringComparison.Ordinal)) return i + 1;
        }
        return null;
    }

    /// <summary>Lower-case letters and digits only: "bằ ng" and "bằng" become the same, as do a joined and a broken line.</summary>
    private static string Squash(string text) =>
        string.Concat(CheckpointSummaryText.Normalize(text).Where(char.IsLetterOrDigit));
}
