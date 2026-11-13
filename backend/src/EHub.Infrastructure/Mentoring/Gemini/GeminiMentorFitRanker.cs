using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Mentoring.Evidence;
using EHub.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EHub.Infrastructure.Mentoring.Gemini;

/// <summary>Bounded, structured batches score pending projects per mentor role; never writes assignments.</summary>
public sealed class GeminiMentorFitRanker(HttpClient client, IOptions<GeminiMentorOptions> options,
    GeminiRequestCoordinator coordinator, TimeProvider clock, ILogger<GeminiMentorFitRanker> logger,
    MentorRankingExecution? execution = null)
    : IGenerativeMentorFitRanker
{
    private const int MaximumResponseBytes = 1024 * 1024;
    // Short evidence IDs allow larger batches while keeping each model call bounded.
    internal const int ProjectsPerRequest = MentorRankingExecution.ProjectsPerBatch;
    public TimeSpan RequestTimeout => execution?.Timeout ?? TimeSpan.FromSeconds(180);

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>>> RankAsync(
        IReadOnlyCollection<MentorRankingProject> projects, IReadOnlyCollection<MentorProfile> mentors,
        MentorRankingScope scope, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = options.Value;
        if (!settings.IsValid()) throw new MentorFitRankerUnavailableException();
        if (projects.Count == 0) return new Dictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>>();
        if (mentors.Count == 0) return projects.ToDictionary(project => project.Id,
            _ => (IReadOnlyDictionary<Guid, MentorFitRanking>)new Dictionary<Guid, MentorFitRanking>());
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(RequestTimeout);
        var callerToken = cancellationToken;
        cancellationToken = deadline.Token;
        try
        {
            // Validate the complete scope before sending any data. Each response must fit the output budget.
            if (projects.Count > GeminiMentorPrompt.MaximumProjects || projects.Select(project => project.Id).Distinct().Count() != projects.Count)
                throw new MentorFitRankerUnavailableException();
            var prompts = projects.OrderBy(project => project.Id).Chunk(ProjectsPerRequest)
                .Select(batch => new GeminiMentorPrompt(batch, mentors, scope)).ToArray();
            var result = new Dictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>>();
            foreach (var prompt in prompts)
            {
                var scores = await RankBatchAsync(prompt, cancellationToken);
                foreach (var score in scores) result.Add(score.Key, score.Value);
                if (execution is not null) await execution.AdvanceAsync(cancellationToken);
            }
            return result;
        }
        catch (OperationCanceledException) when (!callerToken.IsCancellationRequested)
        { throw new MentorFitRankerUnavailableException(); }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException or
            KeyNotFoundException or ArgumentException or RegexMatchTimeoutException or IOException)
        { throw new MentorFitRankerUnavailableException(); }
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>>> RankBatchAsync(
        GeminiMentorPrompt prompt, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        try
        {
            var cacheKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                string.Join(',', settings.Models) + "\n" + GeminiMentorPrompt.Instruction + "\n" + prompt.Json)));
            await coordinator.Gate.WaitAsync(cancellationToken);
            try
            {
                var now = clock.GetUtcNow();
                if (coordinator.Cache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAt > now)
                    return ParseResults(cached.Json, cached.Model, prompt);
                if (coordinator.ProjectBlockedUntil > now) throw new MentorFitRankerUnavailableException();
                foreach (var model in settings.Models)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    now = clock.GetUtcNow();
                    if (coordinator.ModelBlockedUntil.TryGetValue(model, out var until) && until > now) continue;
                    using var response = await SendRequestAsync(model, prompt, cancellationToken);
                    var body = await ReadBoundedAsync(response, cancellationToken);
                    if (!response.IsSuccessStatusCode)
                    {
                        // No raw response, prompt, key, exception message or HTTP headers are logged.
                        logger.LogWarning("Gemini mentor ranking returned HTTP {StatusCode} for {Model}", (int)response.StatusCode, model);
                        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
                        {
                            coordinator.ModelBlockedUntil[model] = clock.GetUtcNow().AddMinutes(1);
                            continue;
                        }
                        if (response.StatusCode == HttpStatusCode.NotFound)
                        {
                            coordinator.ModelBlockedUntil[model] = clock.GetUtcNow().AddMinutes(10);
                            continue;
                        }
                        if (response.StatusCode == HttpStatusCode.TooManyRequests)
                        {
                            var retryAt = RetryAt(body, response, clock.GetUtcNow());
                            if (IsModelSpecificQuota(body, model, out var daily))
                            {
                                var resetAt = daily ? NextPacificMidnight(clock.GetUtcNow()) : retryAt;
                                coordinator.ModelBlockedUntil[model] = resetAt > retryAt ? resetAt : retryAt;
                                continue;
                            }
                            // Ambiguous/shared quota cannot safely be treated as a per-model allowance.
                            coordinator.ProjectBlockedUntil = retryAt;
                        }
                        throw new MentorFitRankerUnavailableException();
                    }
                    using var envelope = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 32 });
                    var candidates = envelope.RootElement.GetProperty("candidates");
                    if (candidates.GetArrayLength() != 1 || candidates[0].GetProperty("finishReason").GetString() != "STOP")
                        throw new MentorFitRankerUnavailableException();
                    var text = string.Concat(candidates[0].GetProperty("content").GetProperty("parts").EnumerateArray()
                        .Where(part => !part.TryGetProperty("thought", out var thought) || thought.ValueKind != JsonValueKind.True)
                        .Select(part => part.TryGetProperty("text", out var value) ? value.GetString() : null));
                    var result = ParseResults(text, model, prompt);
                    foreach (var expired in coordinator.Cache.Where(item => item.Value.ExpiresAt <= clock.GetUtcNow()).Select(item => item.Key).ToArray())
                        coordinator.Cache.Remove(expired);
                    if (coordinator.Cache.Count >= 32)
                        coordinator.Cache.Remove(coordinator.Cache.MinBy(item => item.Value.ExpiresAt).Key);
                    coordinator.Cache[cacheKey] = new GeminiCachedResponse(text, model, clock.GetUtcNow().AddMinutes(10));
                    logger.LogInformation("Gemini mentor ranking completed using {Model}: {ProjectCount} projects, {MentorCount} profiles; input {InputTokens}, output {OutputTokens}, thoughts {ThoughtTokens}, total {TotalTokens} tokens",
                        model, prompt.Projects.Length, prompt.Mentors.Length, UsageCount(envelope.RootElement, "promptTokenCount"),
                        UsageCount(envelope.RootElement, "candidatesTokenCount"), UsageCount(envelope.RootElement, "thoughtsTokenCount"),
                        UsageCount(envelope.RootElement, "totalTokenCount"));
                    return result;
                }
                throw new MentorFitRankerUnavailableException();
            }
            finally { coordinator.Gate.Release(); }
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException or
            KeyNotFoundException or ArgumentException or RegexMatchTimeoutException or IOException)
        { throw new MentorFitRankerUnavailableException(); }
    }

    private async Task<HttpResponseMessage> SendRequestAsync(string model, GeminiMentorPrompt prompt, CancellationToken ct)
    {
        var settings = options.Value;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var wait = coordinator.NextRequestAt - clock.GetUtcNow();
            if (wait > TimeSpan.Zero) await Task.Delay(wait, clock, ct);
            coordinator.NextRequestAt = clock.GetUtcNow().AddSeconds(settings.MinimumRequestIntervalSeconds);
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent");
            request.Headers.Add("x-goog-api-key", settings.ApiKey);
            request.Content = JsonContent.Create(new
            {
                systemInstruction = new { parts = new[] { new { text = GeminiMentorPrompt.Instruction } } },
                contents = new[] { new { role = "user", parts = new[] { new { text = prompt.Json } } } },
                generationConfig = new
                {
                    temperature = model.StartsWith("gemini-2.5-", StringComparison.Ordinal) ? 0 : 1,
                    maxOutputTokens = 16_384,
                    responseMimeType = "application/json",
                    responseJsonSchema = ResponseSchema(),
                    thinkingConfig = ThinkingConfiguration(model)
                }
            });
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.StatusCode != HttpStatusCode.ServiceUnavailable || attempt == 1) return response;
            // One transient retry, within the caller's deadline and the same request pacing.
            response.Dispose();
            var retryAt = clock.GetUtcNow().AddSeconds(1);
            if (retryAt > coordinator.NextRequestAt) coordinator.NextRequestAt = retryAt;
        }
        throw new MentorFitRankerUnavailableException();
    }

    private static IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>> ParseResults(
        string json, string model, GeminiMentorPrompt prompt)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
        var rows = document.RootElement.GetProperty("results");
        if (rows.GetArrayLength() != prompt.Projects.Length) throw new MentorFitRankerUnavailableException();
        var projects = prompt.Projects.ToDictionary(project => project.Id, StringComparer.Ordinal);
        var mentors = prompt.Mentors.ToDictionary(mentor => mentor.Id, StringComparer.Ordinal);
        var result = new Dictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>>();
        foreach (var row in rows.EnumerateArray())
        {
            var projectId = row.GetProperty("project").GetString();
            if (projectId is null || !projects.TryGetValue(projectId, out var project) || result.ContainsKey(project.SourceId))
                throw new MentorFitRankerUnavailableException();
            var scores = prompt.Mentors.ToDictionary(mentor => mentor.SourceId, _ =>
                new MentorFitRanking(0, [$"Gemini ({model}): chưa có đề xuất chuyên môn phù hợp; cần admin xem lại"], false, 0)
                    { Criteria = MentorProfileFitCriteria.Empty });
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var ranked = row.GetProperty("mentors");
            if (ranked.GetArrayLength() > 4) throw new MentorFitRankerUnavailableException();
            foreach (var candidate in ranked.EnumerateArray())
            {
                var id = candidate.GetProperty("id").GetString();
                if (id is null || !mentors.TryGetValue(id, out var mentor) || !seen.Add(id))
                    throw new MentorFitRankerUnavailableException();
                counts[mentor.Role] = counts.GetValueOrDefault(mentor.Role) + 1;
                if (counts[mentor.Role] > 2)
                    throw new MentorFitRankerUnavailableException();
                var criteria = candidate.GetProperty("criteria");
                var reasons = new List<string> { $"Gemini ({model}): đánh giá 5 tiêu chí hồ sơ" };
                var values = new[] { "experience", "expertise", "startupDomain", "technology", "bio" }
                    .Select(name => ReadCriterion(criteria.GetProperty(name), mentor, name, reasons)).ToArray();
                if (values.All(value => value == 0)) throw new MentorFitRankerUnavailableException();
                // No overall model score exists. Application computes the weighted profile score from these five inputs.
                scores[mentor.SourceId] = new MentorFitRanking(0, reasons.ToArray(), true, 0)
                    { Criteria = new(values[0], values[1], values[2], values[3], values[4]) };
            }
            result.Add(project.SourceId, scores);
        }
        return result;
    }

    private static int ReadCriterion(JsonElement criterion, GeminiMentor mentor, string name, List<string> reasons)
    {
        if (!criterion.GetProperty("score").TryGetInt32(out var score) || score is < 0 or > 100)
            throw new MentorFitRankerUnavailableException();
        var quote = criterion.GetProperty("evidence").GetString();
        if (score == 0 && quote == "") return 0;
        if (quote is not null && mentor.EvidenceOptions[name].TryGetValue(quote, out var selected)) quote = selected;
        if (score == 0 || !IsPositiveQuote(mentor.Fields[name], quote, 2, 2))
            throw new MentorFitRankerUnavailableException();
        reasons.Add($"Gemini {name}: {score}/100; bằng chứng: {quote}");
        return score;
    }

    private static bool IsPositiveQuote(string source, string? quote, int minimumLength, int minimumLetters) =>
        !string.IsNullOrWhiteSpace(quote) && quote.Length >= minimumLength && quote.Length <= 160 &&
        !quote.Contains("[removed]", StringComparison.Ordinal) && source.Contains(quote, StringComparison.Ordinal) &&
        quote.Count(char.IsLetter) >= minimumLetters && MentorEvidenceText.Excluded(quote).Length == 0 &&
        MentorEvidenceText.Positive(source).Contains(MentorEvidenceText.Positive(quote), StringComparison.Ordinal);

    private static object ThinkingConfiguration(string model) => model == "gemini-2.5-flash-lite"
        ? new { thinkingBudget = 0 } : (object)new { thinkingLevel = model == "gemini-3.5-flash-lite" ? "minimal" : "low" };

    private static int? UsageCount(JsonElement envelope, string field) =>
        envelope.TryGetProperty("usageMetadata", out var usage) && usage.ValueKind == JsonValueKind.Object &&
        usage.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var count) && count >= 0 ? count : null;

    private static object ResponseSchema() => new
    {
        type = "object", required = new[] { "results" },
        properties = new
        {
            results = new
            {
                type = "array", items = new
                {
                    type = "object", required = new[] { "project", "mentors" },
                    properties = new
                    {
                        project = new { type = "string" },
                        mentors = new
                        {
                            type = "array", maxItems = 2, items = new
                            {
                                type = "object", required = new[] { "id", "criteria" },
                                properties = new
                                {
                                    id = new { type = "string" },
                                    criteria = new
                                    {
                                        type = "object", required = new[] { "experience", "expertise", "startupDomain", "technology", "bio" },
                                        properties = new { experience = CriterionSchema(), expertise = CriterionSchema(),
                                            startupDomain = CriterionSchema(), technology = CriterionSchema(), bio = CriterionSchema() }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    };

    private static object CriterionSchema() => new
    {
        type = "object", required = new[] { "score", "evidence" },
        properties = new { score = new { type = "integer", minimum = 0, maximum = 100 },
            evidence = new { type = "string", maxLength = 3 } }
    };

    private static async Task<byte[]> ReadBoundedAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.Content.Headers.ContentLength > MaximumResponseBytes) throw new MentorFitRankerUnavailableException();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        await using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) != 0)
        {
            if (buffer.Length + read > MaximumResponseBytes) throw new MentorFitRankerUnavailableException();
            await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
        }
        return buffer.ToArray();
    }

    private static bool IsModelSpecificQuota(byte[] body, string model, out bool daily)
    {
        daily = false;
        using var document = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 16 });
        if (!document.RootElement.GetProperty("error").TryGetProperty("details", out var details)) return false;
        var found = false;
        foreach (var detail in details.EnumerateArray())
        {
            if (!detail.TryGetProperty("@type", out var type) ||
                type.GetString() != "type.googleapis.com/google.rpc.QuotaFailure") continue;
            if (!detail.TryGetProperty("violations", out var violations) || violations.GetArrayLength() == 0) return false;
            foreach (var violation in violations.EnumerateArray())
            {
                if (!violation.TryGetProperty("quotaDimensions", out var dimensions) ||
                    !dimensions.TryGetProperty("model", out var name) || name.GetString()?.Replace("models/", "", StringComparison.Ordinal) != model)
                    return false;
                found = true;
                if (violation.TryGetProperty("quotaId", out var id)) daily |= id.GetString()?.Contains("PerDay", StringComparison.OrdinalIgnoreCase) == true;
            }
        }
        return found;
    }

    private static DateTimeOffset RetryAt(byte[] body, HttpResponseMessage response, DateTimeOffset now)
    {
        var seconds = response.Headers.RetryAfter?.Delta?.TotalSeconds ??
            (response.Headers.RetryAfter?.Date - now)?.TotalSeconds ?? 60;
        using var document = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 16 });
        if (document.RootElement.GetProperty("error").TryGetProperty("details", out var details))
            foreach (var detail in details.EnumerateArray())
                if (detail.TryGetProperty("@type", out var type) && type.GetString() == "type.googleapis.com/google.rpc.RetryInfo" &&
                    detail.TryGetProperty("retryDelay", out var delay) && delay.ValueKind == JsonValueKind.String &&
                    double.TryParse(delay.GetString()?.TrimEnd('s'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed) && double.IsFinite(parsed))
                    seconds = Math.Max(seconds, parsed);
        seconds = Math.Max(seconds, 1);
        return seconds >= (DateTimeOffset.MaxValue - now).TotalSeconds ? DateTimeOffset.MaxValue : now.AddSeconds(seconds);
    }

    private static DateTimeOffset NextPacificMidnight(DateTimeOffset now)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");
        var local = TimeZoneInfo.ConvertTime(now, zone);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local.Date.AddDays(1), DateTimeKind.Unspecified), zone));
    }
}
