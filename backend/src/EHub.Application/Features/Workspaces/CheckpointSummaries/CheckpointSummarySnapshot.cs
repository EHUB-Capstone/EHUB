using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

// Stored only in existing JSON columns; checklist metadata is not added to the public response.
internal static class CheckpointSummarySnapshot
{
    private const string Metadata = "_checklistFingerprint";
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Fingerprint(IReadOnlyList<ChecklistItem> checklist) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(checklist, Options)));

    public static string Serialize<T>(T content, IReadOnlyList<ChecklistItem> checklist, bool newGeneration = false)
    {
        var json = JsonSerializer.SerializeToNode(content, Options)!.AsObject();
        json[Metadata] = Fingerprint(checklist);
        if (newGeneration) json["_generationId"] = Guid.NewGuid().ToString("D");
        return json.ToJsonString(Options);
    }

    public static Guid? GenerationId(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("_generationId", out var value) && value.ValueKind == JsonValueKind.String &&
                Guid.TryParse(value.GetString(), out var id) ? id : null;
        }
        catch (JsonException) { return null; }
    }

    public static bool Matches(string? json, string fingerprint)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty(Metadata, out var value) &&
                value.ValueKind == JsonValueKind.String && value.GetString() == fingerprint;
        }
        catch (JsonException) { return false; }
    }
}
