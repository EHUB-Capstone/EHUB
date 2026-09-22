using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using EHub.Application.Common.Interfaces.AI;
using EHub.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace EHub.Infrastructure.Services;

public sealed partial class GeminiEmbeddingProvider : IEmbeddingProvider
{
    private const int MaximumBatchSize = 20;
    private const string SimilarityPrefix = "task: sentence similarity | query: ";

    private readonly HttpClient _httpClient;
    private readonly ProposalEmbeddingOptions _options;

    public GeminiEmbeddingProvider(HttpClient httpClient, IOptions<ProposalEmbeddingOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public string ProviderName => "GoogleGemini";
    public string ModelName => _options.Model;
    public int Dimension => _options.Dimension;

    public async Task<IReadOnlyList<EmbeddingVector>> GenerateAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        ValidateConfiguration();
        var output = new List<EmbeddingVector>(texts.Count);

        foreach (var batch in texts.Chunk(MaximumBatchSize))
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"v1beta/models/{_options.Model}:batchEmbedContents");
            request.Headers.Add("x-goog-api-key", _options.ApiKey);
            request.Content = JsonContent.Create(new BatchEmbedRequest(batch.Select(text => new EmbedRequest(
                $"models/{_options.Model}",
                new EmbedContent([new EmbedPart(SimilarityPrefix + text)]),
                _options.Dimension)).ToArray()));

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, cancellationToken);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw Failure("PROPOSAL_EMBEDDING_TIMEOUT", true, exception);
            }
            catch (HttpRequestException exception)
            {
                throw Failure("PROPOSAL_EMBEDDING_UNAVAILABLE", true, exception);
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                    throw Failure(
                        "PROPOSAL_EMBEDDING_PROVIDER_REJECTED",
                        IsTransient(response.StatusCode));

                BatchEmbedResponse? payload;
                try
                {
                    payload = await response.Content.ReadFromJsonAsync<BatchEmbedResponse>(cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    throw Failure("PROPOSAL_EMBEDDING_RESPONSE_INVALID", false, exception);
                }

                if (payload?.Embeddings == null || payload.Embeddings.Count != batch.Length)
                    throw Failure("PROPOSAL_EMBEDDING_RESPONSE_INVALID", false);

                output.AddRange(payload.Embeddings.Select(embedding =>
                    new EmbeddingVector(embedding.Values ?? Array.Empty<float>())));
            }
        }

        return output;
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey)
            || string.IsNullOrWhiteSpace(_options.Model)
            || !ModelNameRegex().IsMatch(_options.Model)
            || _options.Dimension is < 128 or > 3072)
            throw Failure("PROPOSAL_EMBEDDING_CONFIGURATION_INVALID", false);
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.RequestTimeout
        || statusCode == HttpStatusCode.TooManyRequests
        || (int)statusCode >= 500;

    private static EmbeddingProviderException Failure(
        string code,
        bool transient,
        Exception? exception = null) => new(
            code,
            "The configured embedding provider request failed.",
            transient,
            exception);

    [GeneratedRegex(@"^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex ModelNameRegex();

    private sealed record BatchEmbedRequest(IReadOnlyList<EmbedRequest> Requests);
    private sealed record EmbedRequest(string Model, EmbedContent Content, int OutputDimensionality);
    private sealed record EmbedContent(IReadOnlyList<EmbedPart> Parts);
    private sealed record EmbedPart(string Text);
    private sealed record BatchEmbedResponse(IReadOnlyList<EmbeddingData> Embeddings);
    private sealed record EmbeddingData(IReadOnlyList<float>? Values);
}
