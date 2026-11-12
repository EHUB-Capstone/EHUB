using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using EHub.Application.Features.Mentoring.Evidence;

namespace EHub.IntegrationTests.Common;

// Deterministic test-only responses. The actual HTTP parser, quote validation and backend policy still run.
// No integration fixture sends professional data or credentials to an external model.
public sealed class SyntheticGeminiHandler : HttpMessageHandler
{
    public List<string> Inputs { get; } = [];
    public HttpStatusCode? FailureStatus { get; init; }
    public const string FailureMarker = "SYNTHETIC_PRIVATE_PROVIDER_FAILURE";
    private static readonly HashSet<string> StopWords = ["and", "the", "for", "with", "from", "have", "has", "this", "that", "are", "was", "will", "trong", "va", "cua", "voi", "cho"];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = await request.Content!.ReadAsStringAsync(cancellationToken);
        using var envelope = JsonDocument.Parse(body);
        using var payload = JsonDocument.Parse(envelope.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString()!);
        var mentors = payload.RootElement.GetProperty("mentors").EnumerateArray().ToArray();
        var fields = new[] { "experience", "expertise", "startupDomain", "technology", "bio" };
        // Inspect decoded values so Unicode escapes cannot hide private text from the assertions.
        Inputs.Add(string.Join("\n", payload.RootElement.GetProperty("projects").EnumerateArray()
            .Select(project => project.GetProperty("text").GetString())
            .Concat(mentors.SelectMany(mentor => fields.Select(field => mentor.GetProperty("profile").GetProperty(field).GetString())))));
        if (FailureStatus is { } failure)
            return new HttpResponseMessage(failure) { Content = JsonContent.Create(new { error = new { message = FailureMarker } }) };
        var results = payload.RootElement.GetProperty("projects").EnumerateArray().Select(project =>
        {
            var projectText = MentorEvidenceText.Positive(project.GetProperty("text").GetString());
            var candidates = mentors.Select(mentor =>
            {
                var criteria = fields.ToDictionary(field => field, field =>
                {
                    var source = mentor.GetProperty("profile").GetProperty(field).GetString() ?? "";
                    var tokens = Regex.Matches(source, @"[\p{L}\p{N}+#.]{2,}").Select(match => match.Value)
                        .Where(value => !StopWords.Contains(MentorEvidenceText.Normalize(value)) && value.Count(char.IsLetter) >= 2 && value.Length <= 160 &&
                            MentorEvidenceText.Excluded(value).Length == 0 &&
                            MentorEvidenceText.Positive(source).Contains(MentorEvidenceText.Normalize(value), StringComparison.Ordinal) &&
                            projectText.Contains(MentorEvidenceText.Normalize(value), StringComparison.Ordinal)).ToArray();
                    return new { score = tokens.Length > 0 ? 75 : 0, evidence = tokens.FirstOrDefault() ?? "" };
                });
                return new { id = mentor.GetProperty("id").GetString(), criteria };
            }).Where(candidate => candidate.criteria.Values.Any(value => value.score > 0)).Take(2).ToArray();
            return new { project = project.GetProperty("id").GetString(), mentors = candidates };
        }).ToArray();
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                candidates = new[] { new { finishReason = "STOP", content = new { parts = new[] { new { text = JsonSerializer.Serialize(new { results }) } } } } }
            })
        };
    }
}
