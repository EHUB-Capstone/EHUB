using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using EHub.Application.Common.Interfaces.AI;

namespace EHub.Infrastructure.Services;

// Offline/test provider. It offers deterministic retrieval without claiming model-level semantic understanding.
public sealed partial class DeterministicLocalEmbeddingProvider : IEmbeddingProvider
{
    public const int VectorDimension = 384;
    public string ProviderName => "DeterministicLocal";
    public string ModelName => "feature-hashing-384-v1";
    public int Dimension => VectorDimension;

    public Task<IReadOnlyList<EmbeddingVector>> GenerateAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        var result = new EmbeddingVector[texts.Count];
        for (var textIndex = 0; textIndex < texts.Count; textIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var vector = new float[VectorDimension];
            var tokens = TokenRegex().Matches(texts[textIndex].Normalize(NormalizationForm.FormC).ToLowerInvariant())
                .Select(match => match.Value)
                .ToArray();

            for (var tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
            {
                AddFeature(vector, tokens[tokenIndex], 1f);
                if (tokenIndex + 1 < tokens.Length)
                    AddFeature(vector, $"{tokens[tokenIndex]}\u001f{tokens[tokenIndex + 1]}", 0.7f);
            }

            if (!vector.Any(value => value != 0))
                vector[0] = 1;
            Normalize(vector);
            result[textIndex] = new EmbeddingVector(vector);
        }

        return Task.FromResult<IReadOnlyList<EmbeddingVector>>(result);
    }

    private static void AddFeature(float[] vector, string feature, float weight)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(feature));
        var index = BitConverter.ToUInt32(hash, 0) % VectorDimension;
        var sign = (hash[4] & 1) == 0 ? 1f : -1f;
        vector[index] += sign * weight;
    }

    private static void Normalize(float[] vector)
    {
        var norm = Math.Sqrt(vector.Sum(value => value * value));
        if (norm <= 0) return;
        for (var index = 0; index < vector.Length; index++)
            vector[index] = (float)(vector[index] / norm);
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();
}
