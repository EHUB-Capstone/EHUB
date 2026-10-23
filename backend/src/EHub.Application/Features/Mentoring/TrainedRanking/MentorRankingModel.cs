using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EHub.Application.Features.Mentoring.TrainedRanking;

/// <summary>A validated, immutable snapshot of a portable linear ranking artifact.</summary>
public sealed class MentorRankingModel
{
    public const int MaximumArtifactBytes = 256 * 1024;
    public const string Schema = "ehub-mentor-fit-v2";

    public static IReadOnlyList<string> FeatureNames { get; } = Array.AsReadOnly(new[]
    {
        "experience_topic_coverage", "expertise_topic_coverage", "domain_topic_coverage",
        "technology_topic_coverage", "bio_topic_coverage", "experience_token_coverage",
        "profile_token_coverage", "project_topic_count_normalized"
    });

    internal IReadOnlyList<RankingTopic> Topics { get; }
    internal IReadOnlySet<string> StopWords { get; }
    internal IReadOnlyList<double> Weights { get; }
    internal double Bias { get; }
    public string TrainingSource { get; }
    public bool SuitabilityValidated { get; }

    private MentorRankingModel(RankingTopic[] topics, HashSet<string> stopWords, double[] weights,
        double bias, string trainingSource, bool suitabilityValidated)
    {
        Topics = Array.AsReadOnly(topics);
        StopWords = stopWords;
        Weights = Array.AsReadOnly(weights);
        Bias = bias;
        TrainingSource = trainingSource;
        SuitabilityValidated = suitabilityValidated;
    }

    public static MentorRankingModel FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > MaximumArtifactBytes)
            throw InvalidModel();

        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 12 });
            var root = document.RootElement;
            RequireObject(root);
            if (!root.TryGetProperty("version", out var version) || !version.TryGetInt32(out var number) || number != 1 ||
                ReadString(root, "featureSchema", 64) != Schema)
                throw InvalidModel();

            var names = ReadArray(root, "featureNames", FeatureNames.Count, FeatureNames.Count)
                .Select(x => ReadStringValue(x, 80)).ToArray();
            if (!names.SequenceEqual(FeatureNames, StringComparer.Ordinal)) throw InvalidModel();

            var weights = ReadArray(root, "weights", FeatureNames.Count, FeatureNames.Count)
                .Select(ReadCoefficient).ToArray();
            if (weights.Take(7).Any(x => x < 0)) throw InvalidModel();
            if (!root.TryGetProperty("bias", out var biasValue)) throw InvalidModel();
            var bias = ReadCoefficient(biasValue);
            var source = ReadString(root, "trainingSource", 80);
            if (!root.TryGetProperty("suitabilityValidated", out var validated) ||
                validated.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw InvalidModel();
            if (source == "synthetic-bootstrap" && validated.GetBoolean()) throw InvalidModel();

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var aliases = new HashSet<string>(StringComparer.Ordinal);
            var topics = ReadArray(root, "topics", 1, 128).Select(item =>
            {
                RequireObject(item);
                var id = ReadString(item, "id", 64);
                if (!Regex.IsMatch(id, @"\A[a-z0-9_-]+\z", RegexOptions.CultureInvariant,
                        TimeSpan.FromMilliseconds(100)) || !ids.Add(id))
                    throw InvalidModel();
                var label = item.TryGetProperty("label", out var labelValue)
                    ? ReadStringValue(labelValue, 120) : id;
                var topicAliases = ReadArray(item, "aliases", 1, 32).Select(value =>
                {
                    var original = ReadStringValue(value, 120);
                    var normalized = MentorRankingFeatures.Normalize(original);
                    if (normalized.Length == 0 || !aliases.Add(normalized)) throw InvalidModel();
                    return new RankingAlias(original, normalized);
                }).ToArray();
                var role = item.TryGetProperty("role", out var roleValue) ? ReadStringValue(roleValue, 16) : "Both";
                if (role is not ("Business" or "IT" or "Both" or "Domain")) throw InvalidModel();
                var parent = item.TryGetProperty("parent", out var parentValue) ? ReadStringValue(parentValue, 64) : null;
                return new RankingTopic(id, label, Array.AsReadOnly(topicAliases), role, parent);
            }).ToArray();

            // Only one parent level is supported; reject dangling links and cycles.
            foreach (var topic in topics.Where(x => x.Parent is not null))
            {
                var parent = topics.SingleOrDefault(x => x.Id == topic.Parent);
                if (parent is null || parent.Parent is not null || parent.Role != topic.Role) throw InvalidModel();
            }

            var stopWords = ReadArray(root, "stopWords", 0, 512)
                .Select(x => MentorRankingFeatures.Normalize(ReadStringValue(x, 80)))
                .ToHashSet(StringComparer.Ordinal);
            return new MentorRankingModel(topics, stopWords, weights, bias, source, validated.GetBoolean());
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            throw InvalidModel();
        }
    }

    private static void RequireObject(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object || element.EnumerateObject()
            .GroupBy(x => x.Name, StringComparer.Ordinal).Any(x => x.Count() != 1))
            throw InvalidModel();
    }

    private static IEnumerable<JsonElement> ReadArray(JsonElement element, string name, int min, int max)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array ||
            value.GetArrayLength() < min || value.GetArrayLength() > max)
            throw InvalidModel();
        return value.EnumerateArray();
    }

    private static string ReadString(JsonElement element, string name, int max) =>
        element.TryGetProperty(name, out var value) ? ReadStringValue(value, max) : throw InvalidModel();

    private static string ReadStringValue(JsonElement value, int max)
    {
        if (value.ValueKind != JsonValueKind.String) throw InvalidModel();
        var text = value.GetString();
        if (string.IsNullOrWhiteSpace(text) || text.Length > max) throw InvalidModel();
        return text;
    }

    private static double ReadCoefficient(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var coefficient) ||
            !double.IsFinite(coefficient) || Math.Abs(coefficient) > 100)
            throw InvalidModel();
        return coefficient;
    }

    private static InvalidDataException InvalidModel() => new("Invalid mentor ranking artifact.");
}

internal sealed record RankingTopic(string Id, string Label, IReadOnlyList<RankingAlias> Aliases, string Role, string? Parent);
internal sealed record RankingAlias(string Original, string Normalized);
