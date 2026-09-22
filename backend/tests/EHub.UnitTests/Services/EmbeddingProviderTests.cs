using System.Net;
using System.Text;
using System.Text.Json;
using EHub.Application.Common.Interfaces.AI;
using EHub.Infrastructure.Options;
using EHub.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace EHub.UnitTests.Services;

public sealed class EmbeddingProviderTests
{
    [Fact]
    public async Task DeterministicProvider_ReturnsStableNormalizedVectors()
    {
        var provider = new DeterministicLocalEmbeddingProvider();

        var vectors = await provider.GenerateAsync(["quản lý rác thải", "quản lý rác thải", "nông nghiệp"]);

        vectors.Should().HaveCount(3);
        vectors[0].Values.Should().Equal(vectors[1].Values);
        vectors[0].Values.Should().HaveCount(DeterministicLocalEmbeddingProvider.VectorDimension);
        Cosine(vectors[0].Values, vectors[1].Values).Should().BeApproximately(1, 0.000001);
        Cosine(vectors[0].Values, vectors[2].Values).Should().BeLessThan(1);
    }

    [Fact]
    public async Task GeminiProvider_SendsKeyInHeaderAndSimilarityBatchContract()
    {
        string? body = null;
        string? apiKey = null;
        Uri? requestUri = null;
        var handler = new StubHandler(async request =>
        {
            requestUri = request.RequestUri;
            apiKey = request.Headers.GetValues("x-goog-api-key").Single();
            body = await request.Content!.ReadAsStringAsync();
            var values = string.Join(',', Enumerable.Range(0, 128).Select(index => index == 0 ? "1" : "0"));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"{{\"embeddings\":[{{\"values\":[{values}]}}]}}", Encoding.UTF8, "application/json")
            };
        });
        var options = Options.Create(new ProposalEmbeddingOptions
        {
            Provider = "Gemini",
            ApiKey = "test-only-key",
            Model = "gemini-embedding-2",
            Dimension = 128
        });
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://generativelanguage.googleapis.com/") };
        var provider = new GeminiEmbeddingProvider(client, options);

        var vectors = await provider.GenerateAsync(["Nội dung proposal"]);

        vectors.Single().Values.Should().HaveCount(128);
        vectors.Single().Values[0].Should().Be(1);
        apiKey.Should().Be("test-only-key");
        requestUri!.Query.Should().BeEmpty();
        requestUri.AbsolutePath.Should().EndWith("/v1beta/models/gemini-embedding-2:batchEmbedContents");
        using var json = JsonDocument.Parse(body!);
        json.RootElement.GetProperty("requests")[0]
            .GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString()
            .Should().Be("task: sentence similarity | query: Nội dung proposal");
        body.Should().Contain("\"outputDimensionality\":128");
        body.Should().Contain("\"model\":\"models/gemini-embedding-2\"");
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, true)]
    [InlineData(HttpStatusCode.InternalServerError, true)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    public async Task GeminiProvider_ClassifiesProviderFailures(HttpStatusCode status, bool transient)
    {
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(status)));
        var options = Options.Create(new ProposalEmbeddingOptions
        {
            Provider = "Gemini",
            ApiKey = "test-only-key",
            Model = "gemini-embedding-2",
            Dimension = 128
        });
        var provider = new GeminiEmbeddingProvider(
            new HttpClient(handler) { BaseAddress = new Uri("https://generativelanguage.googleapis.com/") },
            options);

        var action = () => provider.GenerateAsync(["proposal"]);

        var exception = await action.Should().ThrowAsync<EmbeddingProviderException>();
        exception.Which.IsTransient.Should().Be(transient);
    }

    private static double Cosine(IReadOnlyList<float> left, IReadOnlyList<float> right) =>
        left.Zip(right).Sum(pair => pair.First * pair.Second)
        / Math.Sqrt(left.Sum(value => value * value) * right.Sum(value => value * value));

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => handler(request);
    }
}
