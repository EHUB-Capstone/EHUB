using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Net.Http.Json;
using EHub.Application.Common.Interfaces.Services;
using EHub.Domain.Entities;
using EHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Pgvector;

namespace EHub.Infrastructure.Mentoring;

public sealed class OllamaMentorEmbeddingSearch(AppDbContext db, HttpClient client, IConfiguration configuration)
    : IMentorEmbeddingSearch
{
    private const int Dimensions = 1024;
    private const int BatchSize = 4;
    private readonly string _model = configuration["Mentoring:EmbeddingModel"] ?? "bge-m3";

    public async Task<IReadOnlyDictionary<Guid, double>> SimilaritiesAsync(string projectText,
        IReadOnlyCollection<MentorProfile> mentors, CancellationToken cancellationToken)
    {
        if (mentors.Count == 0 || string.IsNullOrWhiteSpace(projectText))
            return new Dictionary<Guid, double>();

        var mentorIds = mentors.Select(x => x.Id).ToArray();
        var cached = await db.MentorEmbeddings.AsNoTracking()
            .Where(x => mentorIds.Contains(x.MentorProfileId))
            .ToDictionaryAsync(x => x.MentorProfileId, cancellationToken);
        var profiles = mentors.Select(x => new ProfileText(x.Id, BuildText(x))).ToArray();
        var missing = profiles.Where(x => !cached.TryGetValue(x.Id, out var entry) ||
            entry.ContentHash != Hash(x.Text) || entry.ModelName != _model).ToArray();

        // Batching keeps request size bounded and lets one Ollama call encode several mentors.
        foreach (var batch in missing.Chunk(BatchSize))
        {
            var vectors = await EmbedAsync(batch.Select(x => x.Text).ToArray(), cancellationToken);
            for (var i = 0; i < batch.Length; i++)
            {
                var item = batch[i];
                var entry = new MentorEmbedding { MentorProfileId = item.Id, ContentHash = Hash(item.Text),
                    ModelName = _model, Embedding = new Vector(vectors[i]), UpdatedAt = DateTime.UtcNow };
                // PostgreSQL upsert handles simultaneous recommendation requests safely.
                await db.Database.ExecuteSqlInterpolatedAsync($@"
                    INSERT INTO mentor_embeddings (mentor_profile_id, content_hash, model_name, embedding, updated_at)
                    VALUES ({entry.MentorProfileId}, {entry.ContentHash}, {entry.ModelName}, {entry.Embedding}, {entry.UpdatedAt})
                    ON CONFLICT (mentor_profile_id) DO UPDATE SET
                        content_hash = EXCLUDED.content_hash,
                        model_name = EXCLUDED.model_name,
                        embedding = EXCLUDED.embedding,
                        updated_at = EXCLUDED.updated_at", cancellationToken);
                cached[item.Id] = entry;
            }
        }

        var projectVector = (await EmbedAsync([projectText[..Math.Min(projectText.Length, 6000)]],
            cancellationToken))[0];
        return profiles.ToDictionary(x => x.Id, x => Cosine(projectVector, cached[x.Id].Embedding.ToArray()));
    }

    private async Task<float[][]> EmbedAsync(string[] texts, CancellationToken cancellationToken)
    {
        JsonDocument json;
        try
        {
            using var response = await client.PostAsJsonAsync("api/embed",
                new { model = _model, input = texts, truncate = false, keep_alive = "30m" }, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        }
        catch (HttpRequestException) { throw new MentorEmbeddingUnavailableException(); }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new MentorEmbeddingUnavailableException(); }
        catch (JsonException) { throw new MentorEmbeddingUnavailableException(); }
        using (json)
        {
            if (json.RootElement.ValueKind != JsonValueKind.Object ||
                !json.RootElement.TryGetProperty("embeddings", out var embeddings) ||
                embeddings.ValueKind != JsonValueKind.Array || embeddings.GetArrayLength() != texts.Length)
                throw new MentorEmbeddingUnavailableException();
            var output = new float[texts.Length][];
            for (var i = 0; i < output.Length; i++)
            {
                var vector = embeddings[i];
                if (vector.ValueKind != JsonValueKind.Array || vector.GetArrayLength() != Dimensions)
                    throw new MentorEmbeddingUnavailableException();
                var values = new float[Dimensions];
                var offset = 0;
                foreach (var element in vector.EnumerateArray())
                {
                    if (element.ValueKind != JsonValueKind.Number || !element.TryGetSingle(out values[offset++]))
                        throw new MentorEmbeddingUnavailableException();
                }
                if (values.Any(x => !float.IsFinite(x)) || values.All(x => x == 0))
                    throw new MentorEmbeddingUnavailableException();
                output[i] = values;
            }
            return output;
        }
    }

    internal static double Cosine(float[] left, float[] right)
    {
        if (left.Length != right.Length || left.Length == 0) throw new ArgumentException("Vector dimensions differ.");
        double dot = 0, leftLength = 0, rightLength = 0;
        for (var i = 0; i < left.Length; i++)
        {
            dot += (double)left[i] * right[i];
            leftLength += (double)left[i] * left[i];
            rightLength += (double)right[i] * right[i];
        }
        return leftLength == 0 || rightLength == 0 ? 0 :
            Math.Clamp(dot / Math.Sqrt(leftLength * rightLength), -1, 1);
    }

    private static string BuildText(MentorProfile profile)
    {
        var text = string.Join('\n', new[] { $"Loại mentor: {profile.MentorType}",
            $"Chuyên môn: {string.Join(", ", profile.Expertise)}", $"Kinh nghiệm: {profile.Experience}",
            $"Giới thiệu: {profile.Bio}" });
        return text[..Math.Min(text.Length, 6000)];
    }

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private sealed record ProfileText(Guid Id, string Text);
}
