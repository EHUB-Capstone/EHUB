using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using EHub.Application.Common.Interfaces.Services;
using EHub.Domain.Entities;

namespace EHub.Application.Features.Mentoring.TrainedRanking;

/// <summary>
/// Feature schema v2. Keep clause filtering, role selection and clipping in sync with the trainer.
/// The default recommendation view selects topics by mentor type. Allocation can evaluate all specialties;
/// identity, employer and workload are not features.
/// </summary>
public static class MentorRankingFeatures
{
    private const int MaximumTextLength = 6000;
    private const int MaximumTokens = 256;
    private static readonly Regex TokenPattern = new(@"[a-z0-9+#.]+", RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    public static double[] Extract(MentorRankingModel model, string projectText, MentorProfile mentor) =>
        ExtractEvidence(model, PrepareProject(model, projectText, mentor.MentorType), mentor).Values;

    internal static ProjectEvidence PrepareProject(MentorRankingModel model, string projectText, string role,
        MentorRankingScope scope = MentorRankingScope.MentorRole)
    {
        var bounded = Clip(projectText, MaximumTextLength);
        var normalized = MentorEvidenceText.Positive(bounded);
        var excluded = FindTopics(model, MentorEvidenceText.Excluded(bounded)).Keys.ToHashSet();
        // Allocation slot/type remains a hard rule in the allocation engine. It is
        // independent of the specialties documented in an eligible mentor's profile.
        var allSpecialties = scope == MentorRankingScope.AllSpecialties;
        var allowed = model.Topics.Where(x => allSpecialties || x.Role == role || x.Role is "Both" or "Domain")
            .Select(x => x.Id).ToHashSet();
        var topics = FindTopics(model, normalized).Where(x => allowed.Contains(x.Key) && !excluded.Contains(x.Key)).ToDictionary();
        var core = topics.Keys.Where(id => model.Topics.Single(x => x.Id == id).Role != "Domain").ToHashSet();
        if (core.Count == 0 && (allSpecialties || role == "Business")) core.UnionWith(topics.Keys);
        return new ProjectEvidence(topics, Tokens(model, WithoutExcludedTopics(model, normalized, excluded)), core);
    }

    internal static RankingEvidence ExtractEvidence(MentorRankingModel model, ProjectEvidence project,
        MentorProfile mentor)
    {
        var experience = Clip(string.Join('\n', new[] { Clip(mentor.Experience, MaximumTextLength) }
            .Concat((mentor.Experiences ?? []).Take(64).Select(x =>
                Clip(x.Area, 256) + " " + Clip(x.Notes, 2000)))), MaximumTextLength);
        var expertise = JoinLabels((mentor.Expertise ?? []).Concat(mentor.Tags ?? []));
        var domain = JoinLabels(mentor.StartupDomains ?? []);
        var technology = JoinLabels(mentor.TechnologySkills ?? []);
        var bio = Clip(mentor.Bio, MaximumTextLength);
        var rawFields = new[] { experience, expertise, domain, technology, bio };
        var fields = rawFields.Select(MentorEvidenceText.Positive).ToArray();
        // A previously inferred tag must not override an explicit denial in a CV.
        var denied = new[] { 0, 4 }.SelectMany(i => FindTopics(model, MentorEvidenceText.Excluded(rawFields[i])).Keys).ToHashSet();
        var fieldTopics = fields.Select(x => FindTopics(model, x)).ToArray();
        foreach (var topics in fieldTopics)
            foreach (var id in denied) topics.Remove(id);
        fields = fields.Select(field => WithoutExcludedTopics(model, field, denied)).ToArray();
        var experienceTokens = Tokens(model, fields[0]);
        var profileTokens = Tokens(model, string.Join(' ', fields));
        var values = new double[8];
        for (var i = 0; i < fieldTopics.Length; i++)
            values[i] = Coverage(project.Topics.Keys, fieldTopics[i].Keys);
        values[5] = Coverage(project.Tokens, experienceTokens);
        values[6] = Coverage(project.Tokens, profileTokens);
        values[7] = Math.Min(project.Topics.Count / 8d, 1d);

        var prefixes = new[]
        {
            "Kinh nghiệm có nội dung liên quan", "Chuyên môn có nội dung liên quan",
            "Lĩnh vực có nội dung liên quan", "Công nghệ có nội dung liên quan",
            "Giới thiệu có nội dung liên quan"
        };
        var reasons = new List<string>();
        var explainedTopics = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < fieldTopics.Length && reasons.Count < 4; i++)
        {
            foreach (var match in fieldTopics[i])
            {
                if (project.Topics.ContainsKey(match.Key) && explainedTopics.Add(match.Key))
                    reasons.Add($"{prefixes[i]}: {match.Value}");
                if (reasons.Count == 4) break;
            }
        }

        if (reasons.Count == 0)
        {
            var sharedTokens = project.Tokens.Where(profileTokens.Contains).Take(4).ToArray();
            if (sharedTokens.Length > 0)
                reasons.Add($"Nội dung liên quan: {string.Join(", ", sharedTokens)}");
        }

        var hasEvidence = project.CoreTopics.Any(id => fieldTopics.Any(topics => topics.ContainsKey(id)));
        var missing = model.Topics.Where(topic => project.CoreTopics.Contains(topic.Id) &&
            !fieldTopics.Any(topics => topics.ContainsKey(topic.Id))).Select(topic => topic.Label).Take(3).ToArray();
        if (hasEvidence && missing.Length > 0)
            reasons.Add($"Chưa tìm thấy bằng chứng trong hồ sơ cho: {string.Join(", ", missing)}");
        if (!hasEvidence) Array.Clear(values, 0, 7);
        return new RankingEvidence(values, reasons.ToArray(), hasEvidence);
    }

    internal static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        // A clipped string can end at a surrogate boundary; replacing invalid sequences keeps
        // normalization safe even for malformed imported text.
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
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = output.Length > 0;
                continue;
            }
            if (pendingSpace) output.Append(' ');
            pendingSpace = false;
            output.Append(character);
        }
        return output.ToString();
    }

    private static string JoinLabels(IEnumerable<string> labels) =>
        Clip(string.Join(' ', labels.Take(64).Select(x => Clip(x?.Trim(), 256))), MaximumTextLength);

    private static string WithoutExcludedTopics(MentorRankingModel model, string text, HashSet<string> excluded)
    {
        foreach (var topic in model.Topics.Where(x => excluded.Contains(x.Id)))
            foreach (var alias in topic.Aliases.OrderByDescending(x => x.Normalized.Length))
                text = Regex.Replace(text, @"(?<![a-z0-9+#.])" + Regex.Escape(alias.Normalized) + @"(?![a-z0-9+#])",
                    " ", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        return text;
    }

    private static string Clip(string? value, int maximum)
    {
        if (value is null) return string.Empty;
        var length = Math.Min(value.Length, maximum);
        if (length > 0 && char.IsHighSurrogate(value[length - 1])) length--;
        return value[..length];
    }

    private static HashSet<string> Tokens(MentorRankingModel model, string normalized)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in TokenPattern.Matches(normalized))
        {
            var token = match.Value.Trim('.');
            if ((token.Length < 3 && token is not ("c#" or "c++")) ||
                !token.Any(char.IsAsciiLetterOrDigit) || model.StopWords.Contains(token)) continue;
            tokens.Add(token);
            if (tokens.Count == MaximumTokens) break;
        }
        return tokens;
    }

    private static Dictionary<string, string> FindTopics(MentorRankingModel model, string normalized)
    {
        normalized = Normalize(normalized);
        var matches = new Dictionary<string, string>(StringComparer.Ordinal);
        var occurrences = new List<(RankingTopic Topic, RankingAlias Alias, int Start, int End)>();
        foreach (var topic in model.Topics)
            foreach (var alias in topic.Aliases)
                foreach (var position in FindTermPositions(normalized, alias.Normalized))
                    occurrences.Add((topic, alias, position, position + alias.Normalized.Length));
        var farthestEnd = -1;
        foreach (var item in occurrences.OrderBy(x => x.Start).ThenByDescending(x => x.End))
        {
            // "React Native" is mobile evidence; its contained word "React" is not
            // independent evidence of React web expertise.
            if (item.End <= farthestEnd) continue;
            farthestEnd = item.End;
            matches.TryAdd(item.Topic.Id, item.Alias.Original);
        }
        foreach (var topic in model.Topics.Where(x => x.Parent is not null && matches.ContainsKey(x.Id)))
            matches.TryAdd(topic.Parent!, model.Topics.Single(x => x.Id == topic.Parent).Label);
        return matches;
    }

    private static IEnumerable<int> FindTermPositions(string text, string term)
    {
        var offset = 0;
        while (offset <= text.Length - term.Length)
        {
            var position = text.IndexOf(term, offset, StringComparison.Ordinal);
            if (position < 0) yield break;
            var end = position + term.Length;
            if ((position == 0 || !IsTermCharacter(text[position - 1])) &&
                (end == text.Length || !(char.IsAsciiLetterOrDigit(text[end]) || text[end] is '+' or '#'))) yield return position;
            offset = position + 1;
        }
    }

    private static bool IsTermCharacter(char value) => char.IsAsciiLetterOrDigit(value) || value is '+' or '#' or '.';

    private static double Coverage(IEnumerable<string> requested, IEnumerable<string> available)
    {
        var requestedSet = requested.ToHashSet(StringComparer.Ordinal);
        if (requestedSet.Count == 0) return 0;
        var availableSet = available.ToHashSet(StringComparer.Ordinal);
        return requestedSet.Count(availableSet.Contains) / (double)requestedSet.Count;
    }
}

internal sealed record ProjectEvidence(Dictionary<string, string> Topics, HashSet<string> Tokens, HashSet<string> CoreTopics);
internal sealed record RankingEvidence(double[] Values, string[] Reasons, bool HasEvidence);
