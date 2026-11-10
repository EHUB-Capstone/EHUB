using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using EHub.Application.Common.Interfaces.Services;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EHub.Infrastructure.Summarization;

/// <summary>
/// Calls a local Ollama chat model with a JSON schema. Settings follow the benchmark in
/// docs/workspace/AI_CHECKPOINT_SUMMARY.md: greedy decoding, 4k context, thinking disabled.
/// </summary>
public sealed class OllamaCheckpointSummarizer(
    HttpClient client, IConfiguration configuration, ILogger<OllamaCheckpointSummarizer>? logger = null) : ICheckpointSummarizer
{
    private readonly int _contextLength = configuration.GetValue("Summarization:ContextLength", 4096);

    // Kept loaded between uploads: reloading the model costs more than the idle memory on the dedicated container.
    private readonly string _keepAlive = configuration["Summarization:KeepAlive"] ?? "30m";

    public string ModelName { get; } = configuration["Summarization:Model"] ?? "qwen3.5:2b-q4_K_M";

    public async Task<Result<string>> CompleteJsonAsync(
        string systemPrompt, string userPrompt, string jsonSchema, CancellationToken cancellationToken)
    {
        var body = new JsonObject
        {
            ["model"] = ModelName,
            ["stream"] = false,
            ["think"] = false,
            ["keep_alive"] = _keepAlive,
            ["format"] = JsonNode.Parse(jsonSchema),
            ["messages"] = new JsonArray(
                new JsonObject { ["role"] = "system", ["content"] = systemPrompt },
                new JsonObject { ["role"] = "user", ["content"] = userPrompt }),
            ["options"] = new JsonObject
            {
                // Greedy decoding with a fixed seed: the same file gives the same summary every time, and a small model
                // invents less when it is not sampling. With 0.2 two runs on one file stressed entirely different parts.
                ["temperature"] = 0,
                ["seed"] = 42,
                ["num_ctx"] = _contextLength,
                // The short v4 answer needs about 200-300 tokens; the cap stops a runaway answer at about 1 minute on CPU.
                ["num_predict"] = 600
            }
        };

        try
        {
            using var response = await client.PostAsJsonAsync("api/chat", body, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                // 404 usually means the model was never pulled into this Ollama.
                logger?.LogWarning("Ollama answered {StatusCode} for model {Model}.", (int)response.StatusCode, ModelName);
                return Unreachable();
            }
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (json.RootElement.ValueKind != JsonValueKind.Object ||
                !json.RootElement.TryGetProperty("message", out var message) ||
                message.ValueKind != JsonValueKind.Object ||
                !message.TryGetProperty("content", out var content) ||
                content.ValueKind != JsonValueKind.String)
                return BadAnswer();
            var text = content.GetString();
            return string.IsNullOrWhiteSpace(text) ? BadAnswer() : Result.Success(text);
        }
        catch (HttpRequestException exception)
        {
            logger?.LogWarning("Ollama is not reachable ({ExceptionType}).", exception.GetType().Name);
            return Unreachable();
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger?.LogWarning("Ollama did not answer in time.");
            return Result.Failure<string>(ErrorCodes.WorkspaceSummaryModelTimeout, "The AI summary service took too long.");
        }
        catch (JsonException) { return BadAnswer(); }
    }

    public async Task WarmUpAsync(CancellationToken cancellationToken)
    {
        try
        {
            // A generate call without a prompt only loads the model and applies keep_alive.
            using var response = await client.PostAsJsonAsync("api/generate",
                new JsonObject { ["model"] = ModelName, ["keep_alive"] = _keepAlive }, cancellationToken);
            if (!response.IsSuccessStatusCode)
                logger?.LogWarning("Ollama could not load model {Model} (status {StatusCode}). Pull it with 'ollama pull {Model}'.",
                    ModelName, (int)response.StatusCode, ModelName);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Ollama may still be starting; the first real request loads the model instead.
            logger?.LogWarning("Ollama is not reachable at start-up ({ExceptionType}); summaries are retried automatically.",
                exception.GetType().Name);
        }
    }

    public async Task<SummarizerHealth> CheckHealthAsync(CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            // The list of pulled models: cheap, and it does not load anything into memory.
            using var response = await client.GetAsync("api/tags", limit.Token);
            if (!response.IsSuccessStatusCode) return new SummarizerHealth(false, false);
            await using var stream = await response.Content.ReadAsStreamAsync(limit.Token);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: limit.Token);
            var available = json.RootElement.ValueKind == JsonValueKind.Object &&
                json.RootElement.TryGetProperty("models", out var models) && models.ValueKind == JsonValueKind.Array &&
                models.EnumerateArray().Any(model => model.TryGetProperty("name", out var name) &&
                    string.Equals(name.GetString(), ModelName, StringComparison.OrdinalIgnoreCase));
            return new SummarizerHealth(true, available);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new SummarizerHealth(false, false);
        }
    }

    private static Result<string> Unreachable() => Result.Failure<string>(ErrorCodes.WorkspaceSummaryModelUnreachable,
        "The AI summary service is temporarily unavailable.");

    private static Result<string> BadAnswer() => Result.Failure<string>(ErrorCodes.WorkspaceSummaryModelBadAnswer,
        "The AI summary service returned an answer that could not be read.");
}

/// <summary>Used when <c>Summarization:Provider</c> is Disabled; requests are refused before reaching it.</summary>
public sealed class DisabledCheckpointSummarizer : ICheckpointSummarizer
{
    public string ModelName => "disabled";

    public Task<Result<string>> CompleteJsonAsync(
        string systemPrompt, string userPrompt, string jsonSchema, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure<string>(ErrorCodes.WorkspaceSummaryDisabled, "AI summaries are not enabled on this server."));

    public Task WarmUpAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<SummarizerHealth> CheckHealthAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new SummarizerHealth(false, false));
}
