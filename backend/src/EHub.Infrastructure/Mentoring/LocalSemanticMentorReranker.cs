using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Mentoring.TrainedRanking;
using EHub.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace EHub.Infrastructure.Mentoring;

/// <summary>A local, offline encoder augments v2; failure never silently switches model.</summary>
public sealed class LocalSemanticMentorReranker(HttpClient client, IConfiguration configuration)
    : IMentorSemanticReranker
{
    private const int BatchSize = 32;
    private const int MaximumResponseBytes = 128 * 1024;
    public const string ModelId = "sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2";
    public const string ModelRevision = "e8f8c211226b894fcb81acc59f3b34ba3efd5f42";
    private readonly Lazy<MentorRankingModel> policyModel = new(() =>
        FileMentorFitRanker.ReadModel(configuration["Mentoring:TrainedModelPath"]));

    public async Task<IReadOnlyDictionary<Guid, MentorFitRanking>> RerankAsync(string projectText,
        IReadOnlyCollection<MentorProfile> mentors, IReadOnlyDictionary<Guid, MentorFitRanking> baseline,
        CancellationToken cancellationToken)
    {
        var output = baseline.ToDictionary();
        if (mentors.Count == 0) return output;
        var threshold = configuration.GetValue("Mentoring:Semantic:PromotionThreshold", 0.50);
        var margin = configuration.GetValue("Mentoring:Semantic:PromotionMargin", 0.08);
        var weight = configuration.GetValue("Mentoring:Semantic:Weight", 0.25);
        if (!double.IsFinite(threshold) || threshold is < 0 or > 1 ||
            !double.IsFinite(weight) || weight is < 0 or > 0.5 ||
            !double.IsFinite(margin) || margin is < 0 or > 1) throw new MentorFitRankerUnavailableException();
        var project = MentorSemanticEvidence.ProjectSnippets(projectText);
        if (project.Length == 0) return output;
        var matches = new Dictionary<Guid, (ScoreItem Item, string[] Evidence)>();

        try
        {
            foreach (var batch in mentors.Chunk(BatchSize))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var candidates = batch.Select((mentor, index) => new Candidate(
                    index.ToString(CultureInfo.InvariantCulture), MentorSemanticEvidence.Experience(mentor))).ToArray();
                using var request = new HttpRequestMessage(HttpMethod.Post, "score")
                { Content = JsonContent.Create(new ScoreRequest("ehub-mentor-semantic-v1", project, candidates)) };
                await request.Content.LoadIntoBufferAsync(1024 * 1024, cancellationToken);
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (!response.IsSuccessStatusCode) throw new MentorFitRankerUnavailableException();
                // Buffer with a strict cap before parsing; a misconfigured local service
                // must not allocate an unbounded response or leak its body to API clients.
                await response.Content.LoadIntoBufferAsync(MaximumResponseBytes, cancellationToken);
                var result = await response.Content.ReadFromJsonAsync<ScoreResponse>(cancellationToken);
                if (result is null || result.Schema != "ehub-mentor-semantic-v1" || result.Model != ModelId ||
                    result.Revision != ModelRevision || result.Items is null ||
                    result.Items.Length != batch.Length || result.Items.Any(x => x is null || string.IsNullOrEmpty(x.Id)) ||
                    result.Items.Select(x => x.Id).Distinct().Count() != batch.Length)
                    throw new MentorFitRankerUnavailableException();
                var byId = result.Items.ToDictionary(x => x.Id);
                for (var index = 0; index < batch.Length; index++)
                {
                    var id = index.ToString(CultureInfo.InvariantCulture);
                    if (!byId.TryGetValue(id, out var item) || !double.IsFinite(item.Similarity) ||
                        item.Similarity is < -1 or > 1 || item.EvidenceIndex < -1 ||
                        item.EvidenceIndex >= candidates[index].Evidence.Length ||
                        (item.EvidenceIndex == -1 && item.Similarity != 0)) throw new MentorFitRankerUnavailableException();
                    matches.Add(batch[index].Id, (item, candidates[index].Evidence));
                }
            }
            foreach (var mentor in mentors)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (item, evidence) = matches[mentor.Id];
                var original = baseline[mentor.Id];
                var runnerUp = mentors.Where(x => x.Type == mentor.Type && x.Id != mentor.Id)
                    .Select(x => matches[x.Id].Item.Similarity).DefaultIfEmpty(0).Max();
                // A new candidate requires relevance and separation from competing
                // specialties. Ambiguous generic similarity must abstain.
                var semanticMatch = item.EvidenceIndex >= 0 && item.Similarity >= threshold &&
                                    item.Similarity - runnerUp >= margin &&
                                    MentorSemanticEvidence.CanPromote(policyModel.Value, projectText, mentor);
                if (!original.HasEvidence && !semanticMatch) continue;
                // This blend is an experimental policy, not learned weights or a probability.
                var originalValue = original.RankingValue ?? original.Score;
                var semanticValue = Math.Clamp(item.Similarity, 0, 1) * 100;
                var value = !original.HasEvidence ? semanticValue : item.EvidenceIndex < 0 ? originalValue :
                    (1 - weight) * originalValue + weight * semanticValue;
                var reasons = original.HasEvidence ? original.Reasons.ToList() :
                    new List<string> { "Gợi ý bổ sung theo ngữ nghĩa; cần kiểm tra chuyên môn trước khi phân công",
                        "Mô hình chưa được kiểm chứng bằng đánh giá ghép mentor thực tế" };
                if (item.EvidenceIndex >= 0)
                    reasons.Insert(0, "Đối chiếu ngữ nghĩa với kinh nghiệm: «" + evidence[item.EvidenceIndex] + "»");
                output[mentor.Id] = new MentorFitRanking((int)Math.Round(value, MidpointRounding.AwayFromZero),
                    reasons.ToArray(), true, value);
            }
            return output;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new MentorFitRankerUnavailableException(); }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException or IOException)
        { throw new MentorFitRankerUnavailableException(); }
    }

    public static Uri ServiceUri(IConfiguration configuration)
    {
        var value = configuration["Mentoring:Semantic:BaseUrl"] ?? "http://127.0.0.1:8091/";
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || !uri.IsLoopback ||
            uri.Scheme != Uri.UriSchemeHttp || uri.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidOperationException("Mentor semantic service must use a loopback HTTP origin.");
        return uri;
    }

    private sealed record Candidate(string Id, string[] Evidence);
    private sealed record ScoreRequest(string Schema, string[] Project, Candidate[] Candidates);
    private sealed record ScoreResponse(string Schema, string Model, string Revision, ScoreItem[] Items);
    private sealed record ScoreItem(string Id, double Similarity, int EvidenceIndex);
}
