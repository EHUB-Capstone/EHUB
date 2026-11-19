using System.Net;
using System.Text;
using System.Text.Json;
using EHub.Application.Common.Interfaces.Services;
using EHub.Infrastructure.Showcase.Assistant;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EHub.ApplicationTests.Features.Showcase;

public sealed class GeminiShowcaseAssistantModelTests
{
    private sealed class StubHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        public List<(Uri Uri, string? ApiKey, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var index = Requests.Count;
            Requests.Add((request.RequestUri!, request.Headers.TryGetValues("x-goog-api-key", out var keys) ? keys.Single() : null,
                await request.Content!.ReadAsStringAsync(cancellationToken)));
            return responses[Math.Min(index, responses.Length - 1)](request);
        }
    }

    private const string UserKey = "AIzaSyTestKeyForUnitTests0123456789";

    private static ShowcaseAssistantOptions Valid() => new() { Enabled = true };

    private static GeminiShowcaseAssistantModel Model(StubHandler handler, ShowcaseAssistantOptions? options = null) =>
        new(new HttpClient(handler), Options.Create(options ?? Valid()), NullLogger<GeminiShowcaseAssistantModel>.Instance);

    private static ShowcaseAssistantModelInput Input() => new("Có dự án nào về giáo dục?",
        [new("user", "chào"), new("assistant", "chào bạn")],
        [new("P1", "EduBot", "Gia sư AI", null, null, null, null, "Mvp", ["Giáo dục"], "FA26", "Team A", 3, 10, 1, false, "2026-11-01")],
        1, new DateOnly(2026, 11, 17));

    private static HttpResponseMessage Ok(string modelJson) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new
        {
            candidates = new[] { new { content = new { parts = new[] { new { text = modelJson } } } } }
        }), Encoding.UTF8, "application/json")
    };

    private static HttpResponseMessage Status(HttpStatusCode code) => new(code) { Content = new StringContent("{}") };

    [Fact]
    public async Task ParsesStructuredAnswer_AndSendsCatalogWithTheCallersOwnKeyInAHeader()
    {
        var handler = new StubHandler(_ => Ok("""{"answer":"Có EduBot","projects":[{"ref":"P1","reason":"Gia sư AI"}],"suggestions":["Còn gì nữa?"]}"""));

        var reply = await Model(handler).AskAsync(Input(), UserKey, CancellationToken.None);

        reply.Answer.Should().Be("Có EduBot");
        reply.Picks.Should().ContainSingle().Which.Should().Be(new ShowcaseAssistantPick("P1", "Gia sư AI"));
        reply.Suggestions.Should().Equal("Còn gì nữa?");
        var request = handler.Requests.Single();
        request.ApiKey.Should().Be(UserKey);
        request.Uri.AbsoluteUri.Should().NotContain(UserKey).And.Contain("gemini-3.5-flash-lite:generateContent");
        request.Body.Should().NotContain(UserKey, "the key travels in a header only, never in the prompt");
        using var body = JsonDocument.Parse(request.Body);
        var contents = body.RootElement.GetProperty("contents");
        contents.GetArrayLength().Should().Be(3);
        contents[1].GetProperty("role").GetString().Should().Be("model");
        var payload = contents[2].GetProperty("parts")[0].GetProperty("text").GetString()!;
        payload.Should().Contain("\"catalogSize\":1").And.Contain("EduBot").And.Contain("Có dự án nào về giáo dục?");
        body.RootElement.GetProperty("generationConfig").GetProperty("responseMimeType").GetString().Should().Be("application/json");
    }

    [Fact]
    public async Task RateLimitedModel_FallsBackToTheNextModel()
    {
        var handler = new StubHandler(_ => Status(HttpStatusCode.TooManyRequests), _ => Ok("""{"answer":"ok","projects":[],"suggestions":[]}"""));

        var reply = await Model(handler).AskAsync(Input(), UserKey, CancellationToken.None);

        reply.Answer.Should().Be("ok");
        handler.Requests.Select(request => request.Uri.AbsolutePath).Should().Equal(
            "/v1beta/models/gemini-3.5-flash-lite:generateContent", "/v1beta/models/gemini-3.1-flash-lite:generateContent");
    }

    [Fact]
    public async Task EveryModelRateLimited_TellsTheUserTheirKeyIsOutOfQuota()
    {
        var handler = new StubHandler(_ => Status(HttpStatusCode.TooManyRequests));

        var act = () => Model(handler).AskAsync(Input(), UserKey, CancellationToken.None);

        (await act.Should().ThrowAsync<ShowcaseAssistantKeyRejectedException>()).Which.UserMessage.Should().Contain("hết hạn mức").And.NotContain(UserKey);
        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task RejectedApiKey_StopsWithoutTryingFallbackModels_AndNeverEchoesTheKey()
    {
        foreach (var status in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden })
        {
            var handler = new StubHandler(_ => Status(status));

            var act = () => Model(handler).AskAsync(Input(), UserKey, CancellationToken.None);

            (await act.Should().ThrowAsync<ShowcaseAssistantKeyRejectedException>()).Which.UserMessage.Should().Contain("API key").And.NotContain(UserKey);
            handler.Requests.Should().ContainSingle();
        }
    }

    [Fact]
    public async Task GoogleReportingAnInvalidKeyWithHttp400_IsTheUsersKeyProblem()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"error":{"code":400,"message":"API key not valid. Please pass a valid API key.","status":"INVALID_ARGUMENT","details":[{"reason":"API_KEY_INVALID"}]}}""")
        });

        var act = () => Model(handler).AskAsync(Input(), UserKey, CancellationToken.None);

        await act.Should().ThrowAsync<ShowcaseAssistantKeyRejectedException>();
        handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task AnyOtherHttp400_IsNotBlamedOnTheKey()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"error":{"code":400,"message":"Request contains an invalid argument.","status":"INVALID_ARGUMENT"}}""")
        });

        var act = () => Model(handler).AskAsync(Input(), UserKey, CancellationToken.None);

        await act.Should().ThrowAsync<ShowcaseAssistantUnavailableException>();
    }

    [Fact]
    public async Task DisabledOrMisconfiguredModels_NeverCallGoogle()
    {
        foreach (var options in new[] { new ShowcaseAssistantOptions(), new ShowcaseAssistantOptions { Enabled = true, Model = "not-approved" } })
        {
            var handler = new StubHandler(_ => Ok("{}"));
            var act = () => Model(handler, options).AskAsync(Input(), UserKey, CancellationToken.None);
            await act.Should().ThrowAsync<ShowcaseAssistantUnavailableException>();
            handler.Requests.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task MalformedModelJson_IsHandledAsUnavailable()
    {
        var handler = new StubHandler(_ => Ok("not json"));

        var act = () => Model(handler).AskAsync(Input(), UserKey, CancellationToken.None);

        await act.Should().ThrowAsync<ShowcaseAssistantUnavailableException>();
        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task BlockedPrompt_WithoutCandidates_GetsAPoliteAnswer()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"promptFeedback":{"blockReason":"SAFETY"}}""", Encoding.UTF8, "application/json")
        });

        var reply = await Model(handler).AskAsync(Input(), UserKey, CancellationToken.None);

        reply.Answer.Should().NotBeNullOrWhiteSpace();
        reply.Picks.Should().BeEmpty();
    }

    [Fact]
    public async Task CallerCancellation_IsNotSwallowed()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var handler = new StubHandler(_ => Ok("{}"));

        var act = () => Model(handler).AskAsync(Input(), UserKey, cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
