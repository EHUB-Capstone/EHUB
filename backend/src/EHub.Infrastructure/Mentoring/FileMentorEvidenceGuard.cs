using System.Security;
using System.Text.Json;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Mentoring.Evidence;
using EHub.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace EHub.Infrastructure.Mentoring;

public sealed class FileMentorEvidenceGuard : IMentorEvidenceGuard
{
    private const int MaximumBytes = 1024 * 1024;
    private readonly Lazy<Topic[]> catalog;

    public FileMentorEvidenceGuard(IConfiguration configuration)
    {
        var explicitPath = configuration["Mentoring:Gemini:RuleCatalogPath"];
        catalog = new Lazy<Topic[]>(() => ReadCatalog(explicitPath), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    // Shared alias lookup for checkpoint evidence; it does not calculate mentor fit scores.
    public IReadOnlySet<string> FindAffirmedTopics(string? text) =>
        FindTopics(catalog.Value, MentorEvidenceText.Positive(text));

    public IReadOnlySet<Guid> FindContradictions(string projectText, IReadOnlyCollection<MentorProfile> mentors,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = new HashSet<Guid>();
        var topics = catalog.Value;
        var requested = FindTopics(topics, MentorEvidenceText.Positive(Clip(projectText, 6000)));
        requested.ExceptWith(FindTopics(topics, MentorEvidenceText.Excluded(Clip(projectText, 6000))));
        foreach (var mentor in mentors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var denied = MentorEvidenceText.Excluded(Clip(mentor.Experience, 6000) + "\n" + Clip(mentor.Bio, 6000));
            if (topics.Where(topic => requested.Contains(topic.Id))
                .Any(topic => topic.Aliases.Any(alias => Positions(denied, alias).Any())))
                result.Add(mentor.Id);
        }
        return result;
    }

    // This catalog contains expertise aliases only. It has no model coefficients, scores or training data.
    private static Topic[] ReadCatalog(string? path)
    {
        try
        {
            using var stream = string.IsNullOrWhiteSpace(path)
                ? typeof(FileMentorEvidenceGuard).Assembly.GetManifestResourceStream("EHub.Mentoring.EvidenceCatalog.json")
                : File.OpenRead(path);
            if (stream is null || stream.Length > MaximumBytes) throw new InvalidDataException();
            using var json = JsonDocument.Parse(stream, new JsonDocumentOptions { MaxDepth = 16 });
            var rows = json.RootElement.GetProperty("topics");
            if (rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() is < 1 or > 256)
                throw new InvalidDataException();
            var topics = rows.EnumerateArray().Select(row =>
            {
                var id = row.GetProperty("id").GetString();
                var aliases = row.GetProperty("aliases");
                if (string.IsNullOrWhiteSpace(id) || id.Length > 100 || aliases.ValueKind != JsonValueKind.Array ||
                    aliases.GetArrayLength() is < 1 or > 128) throw new InvalidDataException();
                var values = aliases.EnumerateArray().Select(item =>
                {
                    var value = item.GetString();
                    if (string.IsNullOrWhiteSpace(value) || value.Length > 256) throw new InvalidDataException();
                    return MentorEvidenceText.Normalize(value);
                }).Distinct(StringComparer.Ordinal).ToArray();
                var parent = row.TryGetProperty("parent", out var parentValue) ? parentValue.GetString() : null;
                return new Topic(id, values, parent);
            }).ToArray();
            var byId = topics.ToDictionary(topic => topic.Id, StringComparer.Ordinal);
            foreach (var topic in topics)
            {
                var visited = new HashSet<string>(StringComparer.Ordinal) { topic.Id };
                var parent = topic.Parent;
                while (parent is not null)
                {
                    if (!visited.Add(parent) || !byId.TryGetValue(parent, out var ancestor)) throw new InvalidDataException();
                    parent = ancestor.Parent;
                }
            }
            return topics;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SecurityException or
            JsonException or InvalidDataException or InvalidOperationException or KeyNotFoundException or ArgumentException)
        {
            throw new MentorFitRankerUnavailableException();
        }
    }

    private static HashSet<string> FindTopics(Topic[] topics, string text)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        var occurrences = topics.SelectMany(topic => topic.Aliases.SelectMany(alias =>
            Positions(text, alias).Select(start => (Topic: topic, Start: start, End: start + alias.Length))));
        var farthestEnd = -1;
        foreach (var item in occurrences.OrderBy(item => item.Start).ThenByDescending(item => item.End))
        {
            // A longer alias such as React Native owns its contained words.
            if (item.End <= farthestEnd) continue;
            farthestEnd = item.End;
            found.Add(item.Topic.Id);
        }
        var byId = topics.ToDictionary(topic => topic.Id, StringComparer.Ordinal);
        foreach (var id in found.ToArray())
        {
            var parent = byId[id].Parent;
            while (parent is not null) { found.Add(parent); parent = byId[parent].Parent; }
        }
        return found;
    }

    private static IEnumerable<int> Positions(string text, string term)
    {
        var offset = 0;
        while (offset <= text.Length - term.Length)
        {
            var position = text.IndexOf(term, offset, StringComparison.Ordinal);
            if (position < 0) yield break;
            var end = position + term.Length;
            if ((position == 0 || !(char.IsAsciiLetterOrDigit(text[position - 1]) || text[position - 1] is '+' or '#' or '.')) &&
                (end == text.Length || !(char.IsAsciiLetterOrDigit(text[end]) || text[end] is '+' or '#')))
                yield return position;
            offset = position + 1;
        }
    }

    private static string Clip(string? text, int maximum) => text is null ? string.Empty : text[..Math.Min(text.Length, maximum)];
    private sealed record Topic(string Id, string[] Aliases, string? Parent);
}
